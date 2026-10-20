<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ProblemBanner from '../../components/ProblemBanner.vue'
import { ApiError } from '../../http/client'
import PublicCatalogSearch from './PublicCatalogSearch.vue'
import PublicCartAccess from './PublicCartAccess.vue'
import PublicCategoryNavigation from './PublicCategoryNavigation.vue'
import PublicProductCard from './PublicProductCard.vue'
import PublicUnitContext from './PublicUnitContext.vue'
import { publicOrderingClient, type RatingSummary } from './client'
import { hydrateCartFromCatalog, loadCart, orderItems, receiptStorage, usePublicCart } from './cart'
import { applyPublicTheme } from './theme'
import { checkoutValidation } from './checkout'
import { canonicalPhone, limitPhone, phoneMask } from '../../utils/phone'
import type {
  Address, Confirmation, Product, PublicCatalog, PublicContext,
  SchedulingSlots, ServiceType, Simulation
} from './types'

type Step = 'catalog' | 'cart' | 'checkout' | 'receipt'
const route = useRoute()
const router = useRouter()
const slug = computed(() => String(route.params.slug))
const tableToken = computed(() => route.params.tableToken ? String(route.params.tableToken) : undefined)
const context = ref<PublicContext>()
const establishmentRating = ref<RatingSummary | null>(null)
const catalog = ref<PublicCatalog>()
const loading = ref(true)
const error = ref<unknown>(null)
const step = ref<Step>('catalog')
const catalogSearch = ref('')
const activeCategoryId = ref<string | null>(null)
const selected = ref<Product>()
const selectedImage = computed(() => selected.value
  ? [...selected.value.images].sort((a, b) => a.order - b.order)[0]
  : undefined)
const quantity = ref(1)
const variationId = ref<string | null>(null)
const notes = ref('')
const selections = reactive<Record<string, string[]>>({})
const selectionQuantities = reactive<Record<string, number>>({})
const compositionError = ref('')
const composerGroupElements: Record<string, HTMLElement> = {}
const cart = usePublicCart()
const simulation = ref<Simulation>()
const scheduling = ref<SchedulingSlots>()
const schedulingMode = ref<'immediate' | 'scheduled'>('immediate')
const scheduledAtUtc = ref('')
const slotsLoading = ref(false)
const simulating = ref(false)
const priceChanged = ref(false)
const submitting = ref(false)
const confirmation = ref<Confirmation>()
const previousReference = ref<string | null>(null)
const idempotencyKey = ref<string>()
let controller: AbortController | undefined
let slotsController: AbortController | undefined
let postalCodeController: AbortController | undefined
let simulationController: AbortController | undefined
let postalCodeTimer: ReturnType<typeof setTimeout> | undefined
let lastPostalCodeLookupAttempt = ''
let postalCodeLookupPromise: Promise<void> | undefined
const postalCodeLookupLoading = ref(false)
const postalCodeLookupMessage = ref('')
const lastPostalCodeValues: Partial<Record<'street' | 'neighborhood' | 'city' | 'state', string>> = {}
const simulatedServiceType = ref<ServiceType>()
const simulatedPostalCode = ref('')
const lookupAddressFields = ['street', 'neighborhood', 'city', 'state'] as const

const customer = reactive({ name: '', phone: '', email: '' })
const address = reactive<Address>({
  label: 'Principal', street: '', number: '', complement: null,
  neighborhood: '', city: '', state: '', postalCode: ''
})
const checkout = reactive({
  serviceType: 'Pickup' as ServiceType,
  couponCode: '',
  paymentMethodId: '',
  receivedAmount: null as number | null
})
const money = (value: number) => new Intl.NumberFormat('pt-BR', {
  style: 'currency', currency: 'BRL'
}).format(value)
const normalizedAddress = (value: Address): Address => ({
  ...value, postalCode: value.postalCode.replace(/\D/g, '')
})
const isLookupFilled = (field: typeof lookupAddressFields[number]) =>
  !!lastPostalCodeValues[field] && address[field] === lastPostalCodeValues[field]
const pricingLabel = (strategy?: string) => ({
  Additive: 'valores somados', HighestPrice: 'considera o maior preço',
  Proportional: 'preço proporcional às frações', NoPriceChange: 'sem alteração de preço'
}[strategy ?? 'Additive'] ?? 'valores somados')
const sortedCategories = computed(() => [...(catalog.value?.categories ?? [])]
  .filter(category => category.isActive)
  .sort((a, b) => a.order - b.order))
const normalizedSearch = computed(() => catalogSearch.value.trim().toLocaleLowerCase('pt-BR'))
const visibleCategories = computed(() => sortedCategories.value
  .filter(category => activeCategoryId.value === null || category.id === activeCategoryId.value)
  .map(category => ({
    ...category,
    products: category.products.filter(product => {
      if (!product.isActive) return false
      if (!normalizedSearch.value) return true
      return [product.name, product.description, product.code]
        .some(value => value?.toLocaleLowerCase('pt-BR').includes(normalizedSearch.value))
    })
  }))
  .filter(category => !normalizedSearch.value || category.products.length > 0))
const visibleProductCount = computed(() => visibleCategories.value
  .reduce((total, category) => total + category.products.length, 0))
const availableServices = computed(() => context.value?.availability ?? [])
const selectedServiceAvailability = computed(() =>
  availableServices.value.find(item => item.serviceType === checkout.serviceType))
const serviceOptions = computed(() => {
  const options = context.value?.table
    ? [{ label: 'Nesta mesa', value: 'Table' as ServiceType }, { label: 'Retirada', value: 'Pickup' as ServiceType }, { label: 'Entrega', value: 'Delivery' as ServiceType }]
    : [{ label: 'Retirada', value: 'Pickup' as ServiceType }, { label: 'Entrega', value: 'Delivery' as ServiceType }]
  return options.map(option => ({
    ...option,
    disable: option.value === 'Table' && availableServices.value.find(item => item.serviceType === option.value)?.isAvailable === false
  }))
})
const activeMethods = computed(() => context.value?.paymentMethods ?? [])
const canCheckout = computed(() => cart.state.items.length > 0 &&
  (schedulingMode.value === 'scheduled'
    ? !!scheduledAtUtc.value && !!scheduling.value?.slots.some(slot => slot.startsAt === scheduledAtUtc.value)
    : selectedServiceAvailability.value?.isAvailable !== false))
const quoteNeedsRecalculation = computed(() => !simulation.value ||
  simulatedServiceType.value !== checkout.serviceType ||
  (checkout.serviceType === 'Delivery' && simulatedPostalCode.value !== address.postalCode.replace(/\D/g, '')))
const postalCodeDigits = computed(() => address.postalCode.replace(/\D/g, ''))
const canChooseCheckout = computed(() => cart.state.items.length > 0 &&
  (selectedServiceAvailability.value?.isAvailable !== false ||
    (scheduling.value?.isEnabled === true && scheduling.value.slots.length > 0)))
const formatOpening = (value: string | null | undefined) => value
  ? new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value))
  : null
const availabilityMessage = (reason: string) => ({
  CalendarException: 'A unidade está fechada excepcionalmente.',
  ServicePaused: 'Esta modalidade está temporariamente pausada.',
  OutsideBusinessHours: 'Estamos fora do horário de atendimento.',
  EstablishmentInactive: 'A unidade não está recebendo pedidos.'
}[reason] ?? 'Esta modalidade não está disponível agora.')
const availabilityNoticeMessage = (reason: string, message: string | null) => {
  return message || availabilityMessage(reason)
}
const pageError = computed(() => {
  const failure = error.value
  const availability = selectedServiceAvailability.value
  if (failure instanceof ApiError && availability?.isAvailable === false &&
      failure.problem.reason === availability.reason) {
    return null
  }
  return failure
})

async function load() {
  controller?.abort()
  controller = new AbortController()
  loading.value = true
  error.value = null
  loadCart(slug.value)
  previousReference.value = receiptStorage.read(slug.value)
  try {
    const [resolvedContext, resolvedCatalog] = await Promise.all([
      publicOrderingClient.context(slug.value, tableToken.value, controller.signal),
      publicOrderingClient.catalog(slug.value, controller.signal)
    ])
    context.value = resolvedContext
    catalog.value = resolvedCatalog
    try { establishmentRating.value = await publicOrderingClient.ratingSummary(slug.value, controller.signal) }
    catch { establishmentRating.value = null }
    hydrateCartFromCatalog(resolvedCatalog)
    const preferred = resolvedContext.table ? 'Table' : 'Pickup'
    checkout.serviceType = resolvedContext.availability?.find(item =>
      item.serviceType === preferred && item.isAvailable)?.serviceType ??
      resolvedContext.availability?.find(item => item.isAvailable &&
        (resolvedContext.table || item.serviceType !== 'Table'))?.serviceType ?? preferred
    checkout.paymentMethodId = resolvedContext.paymentMethods[0]?.id ?? ''
    applyPublicTheme(resolvedContext)
    await loadSchedulingSlots()
  } catch (failure) { error.value = failure } finally { loading.value = false }
}
async function loadSchedulingSlots() {
  slotsController?.abort()
  scheduling.value = undefined
  scheduledAtUtc.value = ''
  if (checkout.serviceType === 'Table') return
  slotsController = new AbortController()
  slotsLoading.value = true
  try {
    scheduling.value = await publicOrderingClient.scheduleSlots(slug.value, checkout.serviceType, slotsController.signal)
    if (!scheduling.value.isEnabled) schedulingMode.value = 'immediate'
  } catch (failure) {
    if (!(failure instanceof Error && failure.name === 'CanceledError')) error.value = failure
  } finally { slotsLoading.value = false }
}
const formatSlot = (value: string) => new Intl.DateTimeFormat('pt-BR', {
  timeZone: scheduling.value?.timeZoneId ?? 'UTC', dateStyle: 'medium', timeStyle: 'short'
}).format(new Date(value))
function openProduct(product: Product) {
  if (product.isAvailable === false) return
  selected.value = product
  quantity.value = 1
  variationId.value = product.variations.filter(x => x.isActive && x.isAvailable !== false)
    .sort((a, b) => a.order - b.order)[0]?.id ?? null
  notes.value = ''
  compositionError.value = ''
  Object.keys(selections).forEach(key => delete selections[key])
  Object.keys(selectionQuantities).forEach(key => delete selectionQuantities[key])
  product.additionalGroups.filter(x => x.isActive).forEach(group => { selections[group.id] = [] })
}
function toggle(groupId: string, additionalId: string, maximum: number) {
  const values = selections[groupId] ?? []
  const index = values.indexOf(additionalId)
  if (index >= 0) { values.splice(index, 1); delete selectionQuantities[`${groupId}:${additionalId}`] }
  else if (selectedCount(groupId) < maximum) { values.push(additionalId); selectionQuantities[`${groupId}:${additionalId}`] = 1 }
}
function isCompositeGroup(groupId: string) {
  const strategy = selected.value?.additionalGroups.find(group => group.id === groupId)?.pricingStrategy
  return strategy === 'HighestPrice' || strategy === 'Proportional'
}
function selectedCount(groupId: string) {
  const values = selections[groupId] ?? []
  return isCompositeGroup(groupId) ? values.length : values.reduce((sum, id) => sum + (selectionQuantities[`${groupId}:${id}`] ?? 1), 0)
}
function setSelectionQuantity(groupId: string, additionalId: string, value: string) {
  const parsed = Number(value)
  if (!Number.isFinite(parsed) || parsed < 1) return
  const max = selected.value?.additionalGroups.find(group => group.id === groupId)?.maximumSelection ?? 1
  const otherCount = selectedCount(groupId) - (selectionQuantities[`${groupId}:${additionalId}`] ?? 1)
  selectionQuantities[`${groupId}:${additionalId}`] = Math.min(Math.floor(parsed), max - otherCount)
}
function setComposerGroupElement(groupId: string, element: unknown) {
  if (element instanceof HTMLElement) composerGroupElements[groupId] = element
}
const composerGroups = computed(() => selected.value?.additionalGroups
  .filter(group => group.isActive).sort((a, b) => a.order - b.order) ?? [])
const requiredComposerGroups = computed(() => composerGroups.value
  .filter(group => group.minimumSelection > 0))
const completedComposerGroups = computed(() => requiredComposerGroups.value.filter(group => {
  const count = selectedCount(group.id)
  return count >= group.minimumSelection && count <= group.maximumSelection
}).length)
const composerUnitPrice = computed(() => {
  const product = selected.value
  if (!product) return 0
  const variation = product.variations.find(item => item.id === variationId.value)
  const chosen = product.additionalGroups.flatMap(group =>
    group.items.filter(item => selections[group.id]?.includes(item.id)).map(item => ({ group, item })))
  const compositionGroup = product.additionalGroups.find(group =>
    group.pricingStrategy === 'HighestPrice' || group.pricingStrategy === 'Proportional')
  const compositionOptions = chosen.filter(item => item.group.id === compositionGroup?.id)
  const base = compositionGroup?.pricingStrategy === 'HighestPrice'
    ? Math.max(...compositionOptions.map(item => item.item.price), 0)
    : compositionGroup?.pricingStrategy === 'Proportional'
      ? compositionOptions.reduce((sum, item) => sum + item.item.price /
        Math.max(compositionOptions.length, 1), 0)
      : variation?.price ?? product.basePrice
  return base + chosen
    .filter(item => (item.group.pricingStrategy ?? 'Additive') === 'Additive')
    .reduce((sum, item) => sum + item.item.price *
      (selectionQuantities[`${item.group.id}:${item.item.id}`] ?? 1), 0)
})
async function addProduct() {
  const product = selected.value
  if (!product || product.isAvailable === false) return
  const invalid = product.additionalGroups.filter(group => group.isActive).find(group => {
    const count = selectedCount(group.id)
    return count < group.minimumSelection || count > group.maximumSelection
  })
  if (invalid) {
    compositionError.value = invalid.name + ': selecione entre ' +
      invalid.minimumSelection + ' e ' + invalid.maximumSelection + '.'
    await nextTick()
    composerGroupElements[invalid.id]?.focus()
    return
  }
  const chosenPairs = new Set(product.additionalGroups.flatMap(group =>
    (selections[group.id] ?? []).map(optionId => `${group.id}:${optionId}`)))
  for (const group of product.additionalGroups) for (const option of group.items) {
    if (!chosenPairs.has(`${group.id}:${option.id}`)) continue
    for (const rule of option.compatibilityRules ?? []) {
      const targetSelected = chosenPairs.has(`${rule.targetGroupId}:${rule.targetAdditionalId}`)
      if (rule.kind === 'Requires' && !targetSelected) {
        compositionError.value = `${option.name} também exige uma opção de outro grupo.`
        return
      }
      if (rule.kind === 'Excludes' && targetSelected) {
        compositionError.value = `${option.name} não pode ser combinada com a opção selecionada.`
        return
      }
    }
  }
  const variation = product.variations.find(item => item.id === variationId.value)
  const additionals = product.additionalGroups.flatMap(group =>
    group.items.filter(item => selections[group.id]?.includes(item.id)).map(item => ({ group, item }))
  )
  if (variation?.isAvailable === false || additionals.some(({ item }) => item.isAvailable === false)) {
    compositionError.value = 'Uma opção selecionada ficou indisponível. Revise a composição.'
    return
  }
  const compositionGroup = product.additionalGroups.find(group => group.pricingStrategy === 'HighestPrice' || group.pricingStrategy === 'Proportional')
  const compositionOptions = additionals.filter(x => x.group.id === compositionGroup?.id)
  const compositionPrice = compositionGroup?.pricingStrategy === 'HighestPrice'
    ? Math.max(...compositionOptions.map(x => x.item.price), 0)
    : compositionGroup?.pricingStrategy === 'Proportional'
      ? compositionOptions.reduce((sum, x) => sum + x.item.price / Math.max(compositionOptions.length, 1), 0)
      : (variation?.price ?? product.basePrice)
  const additivePrice = additionals.filter(x => (x.group.pricingStrategy ?? 'Additive') === 'Additive').reduce((sum, x) => sum + x.item.price * (selectionQuantities[`${x.group.id}:${x.item.id}`] ?? 1), 0)
  cart.add({
    key: crypto.randomUUID(), productId: product.id, variationId: variation?.id ?? null,
    productName: product.name, variationName: variation?.name ?? null,
    displayedUnitPrice: compositionPrice + additivePrice,
    quantity: quantity.value, notes: notes.value.trim() || null,
    additionals: additionals.map(({ group, item }) => {
      const fractional = group.pricingStrategy === 'HighestPrice' || group.pricingStrategy === 'Proportional'
      const count = additionals.filter(x => x.group.id === group.id).length
      return { additionalId: item.id, quantity: fractional ? 1 : selectionQuantities[`${group.id}:${item.id}`] ?? 1, groupId: group.id,
        portionNumerator: fractional ? 1 : null, portionDenominator: fractional ? count : null }
    })
  })
  selected.value = undefined
}
function request(deliveryAddress: Address | null = null, scheduledAtOverride?: string | null) {
  return {
    serviceType: checkout.serviceType,
    customerId: null,
    customerAddressId: null,
    tableToken: checkout.serviceType === 'Table' ? context.value?.table?.token ?? null : null,
    deliveryAddress: deliveryAddress ? normalizedAddress(deliveryAddress) : null,
    couponCode: checkout.couponCode.trim() || null,
    paymentMethodId: checkout.paymentMethodId || null,
    items: orderItems(cart.state.items),
    scheduledAtUtc: scheduledAtOverride !== undefined ? scheduledAtOverride :
      schedulingMode.value === 'scheduled' ? scheduledAtUtc.value : null
  }
}
async function simulate() {
  if (!cart.state.items.length) return
  simulationController?.abort()
  const requestController = new AbortController()
  simulationController = requestController
  const requestedServiceType = checkout.serviceType
  const requestedPostalCode = requestedServiceType === 'Delivery' ? address.postalCode.replace(/\D/g, '') : ''
  simulating.value = true
  error.value = null
  try {
    const fallbackSlot = selectedServiceAvailability.value?.isAvailable === false && scheduling.value?.slots.length
      ? scheduling.value.slots[0]?.startsAt ?? null : undefined
    const result = await publicOrderingClient.simulate(
      slug.value,
      request(requestedServiceType === 'Delivery' ? address : null, fallbackSlot),
      requestController.signal
    )
    if (requestController.signal.aborted) return
    priceChanged.value = simulation.value
      ? simulation.value.total !== result.total || simulation.value.subtotal !== result.subtotal
      : result.subtotal !== cart.displayedTotal.value
    simulation.value = result
    simulatedServiceType.value = requestedServiceType
    simulatedPostalCode.value = requestedPostalCode
  } catch (failure) {
    if (!requestController.signal.aborted) { error.value = failure; simulation.value = undefined }
  } finally {
    if (simulationController === requestController) simulating.value = false
  }
}
function removeCartItem(itemKey: string) {
  cart.remove(itemKey)
  if (!cart.state.items.length) {
    simulationController?.abort()
    simulation.value = undefined
    priceChanged.value = false
    simulating.value = false
    step.value = 'catalog'
    return
  }
  void simulate()
}
function cartAdditionalLabels(item: (typeof cart.state.items)[number]) {
  const product = catalog.value?.categories.flatMap(category => category.products)
    .find(candidate => candidate.id === item.productId)
  return item.additionals.flatMap(selection => {
    const group = product?.additionalGroups.find(candidate => candidate.id === selection.groupId) ??
      product?.additionalGroups.find(candidate => candidate.items.some(option => option.id === selection.additionalId))
    const option = group?.items.find(candidate => candidate.id === selection.additionalId)
    if (!option) return []
    const amount = selection.portionNumerator !== null && selection.portionDenominator
      ? `${selection.portionNumerator}/${selection.portionDenominator} `
      : selection.quantity > 1 ? `${selection.quantity}× ` : ''
    return [`${amount}${option.name}`]
  })
}
async function lookupPostalCode(postalCode: string) {
  if (postalCodeLookupLoading.value && lastPostalCodeLookupAttempt === postalCode) return
  const lookupController = new AbortController()
  postalCodeController = lookupController
  lastPostalCodeLookupAttempt = postalCode
  postalCodeLookupLoading.value = true
  postalCodeLookupMessage.value = ''
  for (const field of lookupAddressFields) {
    address[field] = ''
    delete lastPostalCodeValues[field]
  }
  const lookup = async () => {
    try {
      const response = await fetch(`https://brasilapi.com.br/api/cep/v1/${postalCode}`, {
        signal: lookupController.signal,
        headers: { Accept: 'application/json' }
      })
      if (response.status === 404) throw new Error('CEP não encontrado.')
      if (!response.ok) throw new Error('Serviço de CEP indisponível.')
      const result = await response.json() as {
        street?: string; neighborhood?: string; city?: string; state?: string
      }
      if (lookupController.signal.aborted || address.postalCode.replace(/\D/g, '') !== postalCode) return
      const suggestions = {
        street: result.street?.trim() ?? '',
        neighborhood: result.neighborhood?.trim() ?? '',
        city: result.city?.trim() ?? '',
        state: result.state?.trim() ?? ''
      }
      for (const field of lookupAddressFields) {
        address[field] = suggestions[field]
        if (suggestions[field]) lastPostalCodeValues[field] = suggestions[field]
      }
      postalCodeLookupMessage.value = 'Endereço localizado. Confira os dados e informe o número.'
    } catch (failure) {
      if (!lookupController.signal.aborted && address.postalCode.replace(/\D/g, '') === postalCode) {
        postalCodeLookupMessage.value = failure instanceof Error && failure.message === 'CEP não encontrado.'
          ? 'Não encontramos esse CEP. Confira o número ou preencha o endereço manualmente.'
          : 'Não foi possível buscar o CEP agora. Você pode preencher o endereço manualmente.'
      }
    } finally {
      if (postalCodeController === lookupController) postalCodeLookupLoading.value = false
    }
  }
  const pendingLookup = lookup()
  postalCodeLookupPromise = pendingLookup
  await pendingLookup
  if (postalCodeLookupPromise === pendingLookup) postalCodeLookupPromise = undefined
}
async function recalculateDelivery() {
  const postalCode = postalCodeDigits.value
  if (checkout.serviceType === 'Delivery' && postalCode.length !== 8) {
    error.value = new Error('Informe um CEP com 8 dígitos para recalcular a entrega.')
    return
  }
  if (checkout.serviceType === 'Delivery') {
    if (postalCodeLookupLoading.value && lastPostalCodeLookupAttempt === postalCode && postalCodeLookupPromise) {
      await postalCodeLookupPromise
    } else if (lastPostalCodeLookupAttempt !== postalCode) {
      await lookupPostalCode(postalCode)
    }
    if (postalCodeDigits.value !== postalCode) return
  }
  await simulate()
}
function handlePostalCodeBlur() {
  const postalCode = postalCodeDigits.value
  if (postalCode.length !== 8 || postalCode === lastPostalCodeLookupAttempt) return
  if (postalCodeTimer) clearTimeout(postalCodeTimer)
  void lookupPostalCode(postalCode)
}
function validateCheckout() {
  return checkoutValidation(
    checkout.serviceType, checkout.paymentMethodId, customer, address
  )
}
function pastePhone(event: ClipboardEvent) {
  const text = event.clipboardData?.getData('text') ?? ''
  if (!/\d/.test(text)) return
  event.preventDefault()
  customer.phone = limitPhone(text)
}
async function confirm() {
  if (submitting.value) return
  if (quoteNeedsRecalculation.value) {
    await recalculateDelivery()
    return
  }
  const validation = validateCheckout()
  if (validation) { error.value = new Error(validation); return }
  submitting.value = true
  error.value = null
  try {
    let customerId: string | null = null
    let customerAddressId: string | null = null
    if (checkout.serviceType !== 'Table') {
      const identified = await publicOrderingClient.customer(slug.value, {
        name: customer.name.trim(), phone: canonicalPhone(customer.phone),
        email: customer.email.trim() || null,
        address: checkout.serviceType === 'Delivery' ? normalizedAddress(address) : null
      })
      customerId = identified.customerId
      customerAddressId = checkout.serviceType === 'Delivery' ? identified.addressId : null
    }
    const deliveryAddress = checkout.serviceType === 'Delivery' ? normalizedAddress(address) : null
    const finalSimulation = await publicOrderingClient.simulate(slug.value, {
      ...request(deliveryAddress), customerId, customerAddressId
    })
    const deliveryQuoteChanged = checkout.serviceType === 'Delivery' && simulation.value && (
      simulation.value.deliveryRegionId !== finalSimulation.deliveryRegionId ||
      simulation.value.deliveryFee !== finalSimulation.deliveryFee ||
      simulation.value.deliveryEstimatedMinutes !== finalSimulation.deliveryEstimatedMinutes
    )
    if (simulation.value && (simulation.value.total !== finalSimulation.total || deliveryQuoteChanged)) {
      simulation.value = finalSimulation
      simulatedServiceType.value = checkout.serviceType
      simulatedPostalCode.value = checkout.serviceType === 'Delivery' ? address.postalCode.replace(/\D/g, '') : ''
      priceChanged.value = true
      error.value = new Error('O total mudou. Confira os valores atualizados e confirme novamente.')
      return
    }
    simulation.value = finalSimulation
    simulatedServiceType.value = checkout.serviceType
    simulatedPostalCode.value = checkout.serviceType === 'Delivery' ? address.postalCode.replace(/\D/g, '') : ''
    idempotencyKey.value ??= crypto.randomUUID()
    const result = await publicOrderingClient.confirm(slug.value, {
      ...request(deliveryAddress), customerId, customerAddressId,
      paymentMethodId: checkout.paymentMethodId,
      receivedAmount: checkout.receivedAmount,
      deliveryRegionId: finalSimulation.deliveryRegionId,
      expectedDeliveryFee: finalSimulation.deliveryFee,
      expectedDeliveryEstimatedMinutes: finalSimulation.deliveryEstimatedMinutes,
      deliveryQuoteIssuedAt: finalSimulation.deliveryQuoteIssuedAt
    }, idempotencyKey.value)
    confirmation.value = result
    receiptStorage.save(slug.value, result.reference)
    cart.clear()
    step.value = 'receipt'
    idempotencyKey.value = undefined
  } catch (failure) {
    if (failure instanceof ApiError && failure.problem.status === 409 && schedulingMode.value === 'scheduled') {
      await loadSchedulingSlots()
      scheduledAtUtc.value = ''
      error.value = new Error('Esse horário acabou de ficar indisponível. O carrinho foi preservado; escolha outro horário.')
      return
    }
    error.value = failure instanceof ApiError && typeof failure.problem.currentTotal === 'number'
      ? new Error(`${failure.message} Total atualizado: ${money(failure.problem.currentTotal)}.`)
      : failure
  } finally { submitting.value = false }
}
function editIntent() {
  idempotencyKey.value = undefined
  priceChanged.value = false
}
watch(() => [cart.state.revision, checkout.serviceType, checkout.couponCode,
  checkout.paymentMethodId, checkout.receivedAmount, customer.name, customer.phone,
  customer.email, ...Object.values(address)], editIntent)
watch(() => checkout.serviceType, async () => {
  schedulingMode.value = 'immediate'
  await loadSchedulingSlots()
})
watch(() => [address.postalCode.replace(/\D/g, ''), checkout.serviceType] as const, ([postalCode, serviceType]) => {
  if (postalCodeTimer) clearTimeout(postalCodeTimer)
  postalCodeController?.abort()
  postalCodeLookupPromise = undefined
  postalCodeLookupLoading.value = false
  postalCodeLookupMessage.value = ''
  lastPostalCodeLookupAttempt = ''
  if (postalCode.length !== 8 || serviceType !== 'Delivery') return
  postalCodeTimer = setTimeout(() => { void lookupPostalCode(postalCode) }, 350)
})
watch(() => [slug.value, tableToken.value], load)
onMounted(load)
onBeforeUnmount(() => {
  controller?.abort(); slotsController?.abort(); postalCodeController?.abort(); simulationController?.abort()
  if (postalCodeTimer) clearTimeout(postalCodeTimer)
})
</script>

<template>
  <q-page id="main-content" class="ordering-page public-menu-page">
    <div v-if="loading" class="state-panel" role="status" aria-live="polite">
      <q-spinner size="42px" /><p>Carregando cardápio…</p>
    </div>
    <div v-else-if="!context || !catalog" class="state-panel">
      <ProblemBanner :error="error" />
      <h1>Pedidos indisponíveis</h1>
      <p>Esta unidade não está aceitando pedidos neste endereço.</p>
      <q-btn label="Tentar novamente" color="primary" @click="load" />
    </div>
    <template v-else>
      <PublicUnitContext
        :context="context"
        :summary="establishmentRating"
      />
      <aside v-if="step === 'catalog' && previousReference" class="resume-order">
        <span>Você tem um pedido recente.</span>
        <q-btn flat label="Retomar acompanhamento"
          @click="router.push('/order/track/' + previousReference)" />
      </aside>
      <ProblemBanner :error="pageError" />
      <q-banner v-if="selectedServiceAvailability?.isAvailable === false" class="availability-notice q-mb-md" role="status">
        <template #avatar><q-icon name="schedule" /></template>
        <strong>{{ availabilityNoticeMessage(selectedServiceAvailability.reason, selectedServiceAvailability.message) }}</strong>
        <span v-if="selectedServiceAvailability.nextOpening">
          Próxima abertura: {{ formatOpening(selectedServiceAvailability.nextOpening) }}.
        </span>
      </q-banner>

      <template v-if="step === 'catalog'">
        <main class="public-menu-content" aria-label="Cardápio">
          <div class="catalog-controls">
            <PublicCatalogSearch v-model="catalogSearch" />
            <PublicCategoryNavigation v-model="activeCategoryId" :categories="sortedCategories" />
          </div>
          <section v-for="category in visibleCategories" :key="category.id" class="category">
            <div class="category-heading"><h2>{{ category.name }}</h2><p v-if="category.description">{{ category.description }}</p></div>
            <div class="product-grid">
              <PublicProductCard
                v-for="product in category.products"
                :key="product.id"
                :product="product"
                :formatted-price="money(product.basePrice)"
                @select="openProduct"
              />
            </div>
          </section>
          <div v-if="visibleProductCount === 0" class="state-panel" role="status">
            <p>{{ normalizedSearch ? 'Nenhum produto encontrado para esta busca.' : 'Nenhum item disponível no momento.' }}</p>
            <q-btn v-if="normalizedSearch" flat label="Limpar busca" @click="catalogSearch = ''" />
            <q-btn v-else flat label="Atualizar cardápio" @click="load" />
          </div>
        </main>
        <PublicCartAccess
          v-if="cart.state.items.length > 0"
          :count="cart.count.value"
          :total="money(cart.displayedTotal.value)"
          :disabled="cart.state.items.length === 0"
          @open="step = 'cart'; simulate()"
        />
      </template>

      <main v-else-if="step === 'cart'" class="flow-panel">
        <header class="cart-heading">
          <h2>Seu carrinho</h2>
          <p>Revise os itens e valores antes de continuar.</p>
        </header>
        <div v-if="!cart.state.items.length" class="cart-empty">
          <q-icon name="shopping_bag" size="32px" aria-hidden="true" />
          <div>
            <strong>Seu carrinho está vazio</strong>
            <p>Escolha algo no cardápio para montar seu pedido.</p>
          </div>
          <q-btn color="primary" label="Voltar ao cardápio" @click="step = 'catalog'" />
        </div>
        <ul v-else class="cart-list">
          <li v-for="item in cart.state.items" :key="item.key" class="cart-item">
            <div class="cart-item__details">
              <strong>{{ item.quantity }}× {{ item.productName }}</strong>
              <small v-if="item.variationName">{{ item.variationName }}</small>
              <div v-if="item.additionals.length" class="cart-item__modifiers">
                <small class="cart-item__modifiers-label">Adicionais</small>
                <span>{{ cartAdditionalLabels(item).join(' · ') }}</span>
              </div>
              <small v-if="item.notes">Observação: {{ item.notes }}</small>
            </div>
            <strong class="cart-item__price">{{ money(item.displayedUnitPrice * item.quantity) }}</strong>
            <button type="button" class="cart-remove" :aria-label="'Remover ' + item.productName"
              @click="removeCartItem(item.key)">
              <q-icon name="delete_outline" size="18px" aria-hidden="true" />
              <span>Remover</span>
            </button>
          </li>
        </ul>
        <div v-if="simulating" role="status">Recalculando totais…</div>
        <div v-else-if="simulation && cart.state.items.length" class="totals" aria-live="polite">
          <span>Subtotal <b>{{ money(simulation.subtotal) }}</b></span>
          <span>Desconto <b>− {{ money(simulation.discount) }}</b></span>
          <span>Taxas <b>{{ money(simulation.fees) }}</b></span>
          <span v-if="checkout.serviceType === 'Delivery' && simulation.deliveryEstimatedMinutes">
            Entrega estimada <b>{{ simulation.deliveryEstimatedMinutes }} min</b>
          </span>
          <span v-if="checkout.serviceType === 'Delivery' && simulation.deliveryRegionName" class="text-grey-7">
            Cobertura: {{ simulation.deliveryRegionName }}
          </span>
          <span class="grand-total">Total atualizado <b>{{ money(simulation.total) }}</b></span>
          <p v-if="priceChanged" role="alert">O total mudou. Confira os valores atualizados antes de confirmar.</p>
        </div>
        <div v-if="cart.state.items.length" class="flow-actions"><q-btn flat label="Voltar ao cardápio" @click="step = 'catalog'" />
          <q-btn label="Continuar" color="primary" :disable="!simulation || !canChooseCheckout"
            @click="step = 'checkout'" /></div>
      </main>

      <main v-else-if="step === 'checkout'" class="flow-panel">
        <h2>Finalizar pedido</h2>
        <q-form @submit="confirm">
          <fieldset v-if="checkout.serviceType !== 'Table'"><legend>Seus dados</legend>
            <q-input v-model="customer.name" label="Nome" autocomplete="name" />
            <q-input v-model="customer.phone" label="Telefone" autocomplete="tel" type="tel"
              :mask="phoneMask(customer.phone)" unmasked-value @paste="pastePhone" />
            <q-input v-model="customer.email" label="E-mail (opcional)" autocomplete="email" type="email" />
          </fieldset>
          <fieldset><legend>Como você quer receber?</legend>
            <q-option-group v-model="checkout.serviceType" :options="serviceOptions" type="radio" />
          </fieldset>
          <fieldset v-if="checkout.serviceType !== 'Table' && scheduling?.isEnabled">
            <legend>Quando prefere receber?</legend>
            <q-option-group v-model="schedulingMode" :options="[
              { label: 'O mais rápido possível', value: 'immediate', disable: selectedServiceAvailability?.isAvailable === false },
              { label: 'Agendar', value: 'scheduled' }
            ]" type="radio" />
            <div v-if="schedulingMode === 'scheduled'">
              <div v-if="slotsLoading" role="status">Buscando horários disponíveis…</div>
              <q-select v-else v-model="scheduledAtUtc" outlined emit-value map-options
                label="Horário disponível" :options="scheduling.slots.map(slot => ({ label: formatSlot(slot.startsAt), value: slot.startsAt }))" />
              <small>Horários no fuso {{ scheduling.timeZoneId }} · pedidos com pelo menos {{ scheduling.minimumAdvanceMinutes }} minutos de antecedência.</small>
            </div>
          </fieldset>
          <fieldset v-if="checkout.serviceType === 'Delivery'"><legend>Endereço de entrega</legend>
            <q-input v-model="address.postalCode" label="CEP" autocomplete="postal-code"
              mask="#####-###" unmasked-value :loading="postalCodeLookupLoading"
              hint="Informe 8 dígitos. O hífen é visual; buscamos o endereço e você atualiza a cotação em Recalcular entrega."
              persistent-hint @blur="handlePostalCodeBlur" />
            <p v-if="postalCodeLookupMessage" class="postal-code-feedback" role="status" aria-live="polite">
              {{ postalCodeLookupMessage }}
            </p>
            <q-input v-model="address.street" label="Rua" autocomplete="address-line1" />
            <q-input v-model="address.number" label="Número" />
            <q-input v-model="address.complement" label="Complemento (opcional)" />
            <q-input v-model="address.neighborhood" label="Bairro" :disable="isLookupFilled('neighborhood')" />
            <q-input v-model="address.city" label="Cidade" :disable="isLookupFilled('city')" />
            <q-input v-model="address.state" label="Estado" maxlength="2" :disable="isLookupFilled('state')" />
          </fieldset>
          <fieldset><legend>Pagamento e desconto</legend>
            <q-select v-model="checkout.paymentMethodId" label="Forma de pagamento"
              emit-value map-options :options="activeMethods.map(x => ({ label: x.name, value: x.id }))" />
            <q-input v-model="checkout.couponCode" label="Cupom (opcional)" />
            <q-input v-if="activeMethods.find(x => x.id === checkout.paymentMethodId)?.allowsChange"
              v-model.number="checkout.receivedAmount" label="Troco para" type="number" min="0" />
          </fieldset>
          <div v-if="simulation" class="grand-total checkout-total">
            Total {{ money(simulation.total) }}
            <small v-if="quoteNeedsRecalculation" role="status">Cotação anterior; recalcule para atualizar a entrega.</small>
            <small v-else-if="priceChanged" role="status">O valor foi atualizado. Confira antes de prosseguir.</small>
          </div>
          <div class="flow-actions"><q-btn flat label="Voltar ao carrinho" @click="step = 'cart'" />
            <q-btn v-if="quoteNeedsRecalculation" type="button" icon="refresh" label="Recalcular entrega" color="primary"
              :loading="simulating" :disable="simulating || !canCheckout ||
                (checkout.serviceType === 'Delivery' && postalCodeDigits.length !== 8)"
              @click="recalculateDelivery" />
            <q-btn v-else type="submit" icon="shopping_cart_checkout"
              :label="priceChanged ? 'Finalizar o pedido' : 'Confirmar pedido'" color="primary"
              :loading="submitting" :disable="submitting || simulating || !canCheckout" /></div>
        </q-form>
      </main>

      <main v-else class="flow-panel receipt" aria-live="polite">
        <h2>Pedido confirmado</h2>
        <p>Pedido nº <strong>{{ confirmation?.number }}</strong></p>
        <p>Total {{ money(confirmation?.total ?? 0) }}</p>
        <p class="public-reference">Referência: {{ confirmation?.reference }}</p>
          <q-btn icon="receipt_long" label="Acompanhar pedido" color="primary"
          @click="router.push('/order/track/' + confirmation?.reference)" />
      </main>
    </template>

    <q-dialog :model-value="!!selected" @update:model-value="value => { if (!value) selected = undefined }">
      <q-card v-if="selected" class="composition-card" :class="{ 'composition-card--with-image': !!selectedImage }">
        <q-card-section class="composer-summary">
          <img v-if="selectedImage" class="composer-product-image" :src="selectedImage.url" alt="">
          <div class="composer-product-heading">
            <p class="composer-kicker">{{ requiredComposerGroups.length ? 'Personalize seu pedido' : 'Seu pedido' }}</p>
            <h2 id="composer-title">{{ selected.name }}</h2>
            <p v-if="selected.description" class="composer-description">{{ selected.description }}</p>
          </div>
          <div v-if="requiredComposerGroups.length" class="composer-progress" role="status">
            <div><span>Etapas obrigatórias</span><strong>{{ completedComposerGroups }} de {{ requiredComposerGroups.length }}</strong></div>
            <q-linear-progress :value="completedComposerGroups / requiredComposerGroups.length" color="primary" track-color="grey-4" rounded />
          </div>
        </q-card-section>
        <q-card-section class="composition-body" aria-labelledby="composer-title">
          <fieldset v-if="selected.variations.filter(x => x.isActive).length"><legend>Escolha uma opção · obrigatório</legend>
            <label v-for="variation in selected.variations.filter(x => x.isActive).sort((a,b) => a.order-b.order)" :key="variation.id">
              <input v-model="variationId" type="radio" :value="variation.id" :disabled="variation.isAvailable === false">
              {{ variation.name }} — {{ money(variation.price) }}<small v-if="variation.isAvailable === false"> · indisponível</small>
            </label>
          </fieldset>
          <fieldset v-for="group in composerGroups" :id="`composer-group-${group.id}`" :key="group.id"
            :ref="element => setComposerGroupElement(group.id, element)" tabindex="-1">
            <legend>
              {{ group.name }} · {{ group.minimumSelection > 0 ? 'obrigatório' : 'opcional' }}
              ({{ selectedCount(group.id) }}/{{ group.maximumSelection }}) · {{ pricingLabel(group.pricingStrategy) }}
            </legend>
            <label v-for="item in group.items.filter(x => x.isActive).sort((a,b) => a.order-b.order)" :key="item.id">
              <input type="checkbox" :checked="selections[group.id]?.includes(item.id)"
                :disabled="item.isAvailable === false || (!selections[group.id]?.includes(item.id) && selectedCount(group.id) >= group.maximumSelection)"
                @change="toggle(group.id, item.id, group.maximumSelection)">
              {{ item.name }} <span>{{ (group.pricingStrategy ?? 'Additive') === 'NoPriceChange' ? 'sem alteração' : money(item.price) }}</span><small v-if="item.isAvailable === false"> · indisponível</small>
              <input v-if="(group.pricingStrategy ?? 'Additive') === 'Additive' && selections[group.id]?.includes(item.id)" type="number" min="1" :max="group.maximumSelection" :value="selectionQuantities[`${group.id}:${item.id}`] ?? 1" aria-label="Quantidade da opção" @click.stop @input="setSelectionQuantity(group.id, item.id, ($event.target as HTMLInputElement).value)">
            </label>
          </fieldset>
          <q-input v-if="selected.allowsNotes" v-model="notes" label="Observações" type="textarea" />
          <q-input v-model.number="quantity" label="Quantidade" type="number" min="1" />
          <p v-if="compositionError" role="alert" class="text-negative">{{ compositionError }}</p>
        </q-card-section>
        <q-card-actions align="right" class="composer-actions">
          <div class="composer-total">
            <span>{{ quantity }} × {{ money(composerUnitPrice) }}</span>
            <strong>{{ money(composerUnitPrice * Math.max(quantity, 1)) }}</strong>
          </div>
          <q-btn flat label="Cancelar" @click="selected = undefined" />
          <q-btn color="primary" icon="add_shopping_cart" label="Adicionar" @click="addProduct" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<style scoped>
.public-menu-page :deep(.ordering-hero) {
  align-items: center;
  width: min(100%, 1100px);
  min-height: 190px;
  margin: 20px auto 24px;
  padding: clamp(22px, 4vw, 40px);
  border: 1px solid var(--oh-border-subtle);
  border-radius: var(--oh-border-radius);
  background: color-mix(in srgb, var(--oh-brand-primary) 8%, var(--oh-surface-raised));
}
.public-menu-page :deep(.ordering-context h1) {
  font-size: clamp(2rem, 5vw, 3.5rem);
  line-height: 1.08;
  letter-spacing: -.025em;
}
.public-menu-page :deep(.ordering-context > p) {
  margin: 10px 0 0;
  color: var(--oh-text-muted);
  font-size: 1.05rem;
}
.public-menu-page :deep(.ordering-logo) {
  width: clamp(64px, 10vw, 96px);
  height: clamp(64px, 10vw, 96px);
  padding: 8px;
  border: 1px solid var(--oh-border-subtle);
  border-radius: 18px;
  background: var(--oh-surface-raised);
}
.public-menu-page :deep(.ordering-unit-mark) {
  display: grid;
  flex: 0 0 clamp(64px, 10vw, 96px);
  width: clamp(64px, 10vw, 96px);
  height: clamp(64px, 10vw, 96px);
  place-items: center;
  border: 1px solid var(--oh-border-subtle);
  border-radius: 18px;
  background: var(--oh-surface-raised);
  color: var(--oh-brand-primary-text);
  font-size: 42px;
}
.public-menu-page :deep(.ordering-unit-label) {
  margin: 0 0 7px;
  color: var(--oh-brand-primary-text);
  font-size: .78rem;
  font-weight: 750;
  letter-spacing: .045em;
}
.public-menu-page :deep(.ordering-table-badge) {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  border: 1px solid var(--oh-border-subtle);
  border-radius: 999px;
  background: var(--oh-surface-raised);
  color: var(--oh-brand-primary-text);
  font-weight: 700;
  white-space: nowrap;
}
.public-menu-page :deep(.resume-order) {
  width: min(100%, 1100px);
  margin-bottom: 20px;
  padding: 12px 18px;
  color: var(--oh-text-primary);
}
.public-menu-page :deep(.resume-order .q-btn) { color: var(--oh-brand-primary-text); font-weight: 700; }
.availability-notice {
  width: min(100%, 1100px);
  margin-right: auto;
  margin-left: auto;
  border: 1px solid color-mix(in srgb, var(--oh-status-warning) 38%, var(--oh-border-subtle));
  border-radius: var(--oh-border-radius);
  background: color-mix(in srgb, var(--oh-status-warning) 12%, var(--oh-surface-raised));
  color: var(--oh-text-primary);
}
.availability-notice :deep(.q-icon) { color: var(--oh-status-warning-text); }
.public-menu-content { width: min(100%, 1100px); margin: 0 auto; }
.public-menu-page :deep(.catalog-controls) { width: 100%; margin-bottom: 30px; }
.public-menu-page :deep(.catalog-search) { gap: 9px; color: var(--oh-text-primary); }
.public-menu-page :deep(.catalog-search input) { min-height: 54px; border-radius: 14px; }
.public-menu-page :deep(.catalog-search input:focus-visible) {
  border-color: var(--oh-brand-primary);
  outline: 3px solid color-mix(in srgb, var(--oh-focus-ring) 32%, transparent);
  outline-offset: 1px;
}
.public-menu-page :deep(.category-navigation) { gap: 10px; padding-bottom: 10px; }
.public-menu-page :deep(.category-navigation button) { padding: 9px 18px; border-radius: 999px; font-weight: 650; }
.public-menu-page :deep(.category-navigation button[aria-pressed='true']) { border-color: var(--oh-brand-primary); background: var(--oh-brand-primary); color: var(--oh-brand-on-primary); }
.category { margin-top: 34px; }
.category-heading { margin-bottom: 16px; }
.category-heading h2 { margin: 0; color: var(--oh-text-primary); font-size: clamp(1.5rem, 3vw, 2rem); letter-spacing: -.02em; }
.category-heading p { margin: 6px 0 0; color: var(--oh-text-muted); }
.public-menu-page :deep(.product-grid) { gap: 18px; }
.public-menu-page :deep(.public-cart-access) { width: min(calc(100vw - 32px), 620px); border-radius: 16px; }
.public-menu-page :deep(.public-cart-access__button) { border-radius: 12px; font-weight: 750; }
.public-menu-page :deep(.public-cart-access__button:disabled) { cursor: not-allowed; opacity: .72; }
.public-menu-page :deep(.state-panel) { border: 1px solid var(--oh-border-subtle); color: var(--oh-text-primary); }
.checkout-total small { display: block; margin-top: 4px; color: var(--oh-text-muted); font-size: .875rem; font-weight: 400; }
.cart-heading { margin-bottom: 20px; }
.cart-heading h2 { color: var(--oh-text-primary); font-size: clamp(1.65rem, 3vw, 2rem); font-weight: 700; letter-spacing: -.025em; }
.cart-heading p { margin: 5px 0 0; color: var(--oh-text-muted); font-size: .9rem; }
.public-menu-page :deep(.cart-list) { margin: 0; }
.public-menu-page :deep(.cart-item) { display: grid; grid-template-columns: minmax(0, 1fr) auto auto; align-items: center; gap: 14px; padding: 16px 0; border-bottom: 1px solid var(--oh-border-subtle); }
.cart-item__details { display: grid; min-width: 0; gap: 5px; }
.cart-item__details strong { overflow-wrap: anywhere; }
.cart-item__price { font-variant-numeric: tabular-nums; }
.cart-remove { display: inline-flex; min-height: 44px; align-items: center; gap: 4px; padding: 4px 8px; border: 0; border-radius: 8px; background: transparent; color: var(--oh-status-danger); font: inherit; cursor: pointer; }
.cart-remove:hover { background: color-mix(in srgb, var(--oh-status-danger) 9%, transparent); }
.cart-remove:focus-visible { outline: 2px solid var(--oh-focus-ring); outline-offset: 2px; }
.cart-item__modifiers { display: grid; gap: 2px; color: var(--oh-text-muted); font-size: .9rem; }
.cart-item__modifiers-label { color: var(--oh-text-primary); font-weight: 650; }
.cart-empty { display: flex; align-items: center; gap: 16px; padding: 22px; border: 1px dashed var(--oh-border-subtle); border-radius: var(--oh-border-radius); color: var(--oh-text-muted); }
.cart-empty > div { flex: 1; }
.cart-empty strong { color: var(--oh-text-primary); }
.cart-empty p { margin: 4px 0 0; font-size: .9rem; }
.public-menu-page :deep(.totals) { margin-top: 8px; padding: 18px 0 0; border-top: 1px solid var(--oh-border-subtle); }
.public-menu-page :deep(.grand-total) { padding-top: 10px; border-top: 1px solid var(--oh-border-subtle); }
@media (max-width: 700px) {
  .public-menu-page :deep(.ordering-hero) { min-height: 0; gap: 16px; margin-top: 12px; padding: 22px 18px; }
  .public-menu-page :deep(.ordering-context) { flex: 1 1 180px; }
  .public-menu-page :deep(.ordering-table-badge) { padding: 8px 12px; }
  .public-menu-page :deep(.product-grid) { grid-template-columns: minmax(0, 1fr); }
  .public-menu-page :deep(.cart-item) { grid-template-columns: minmax(0, 1fr) auto; align-items: center; gap: 8px; }
  .cart-item__price { grid-column: 1; grid-row: 2; justify-self: start; }
  .cart-remove { grid-column: 2; grid-row: 1 / span 2; }
  .cart-empty { align-items: flex-start; flex-wrap: wrap; }
  .cart-empty > div { flex-basis: calc(100% - 52px); }
  .cart-empty :deep(.q-btn) { width: 100%; }
}
</style>
