import { computed, reactive } from 'vue'
import type { OrderItemRequest, PublicCatalog } from './types'

const VERSION = 2
const PREFIX = 'zfome.public-cart.'
export interface CartItem extends OrderItemRequest {
  key: string
  productName: string
  variationName: string | null
  displayedUnitPrice: number
}
interface PersistedItem extends OrderItemRequest { key: string }
interface PersistedCart { version: number; slug: string; items: PersistedItem[] }
const state = reactive({ slug: '', items: [] as CartItem[], revision: 0 })
const storage = () => typeof window === 'undefined' ? undefined : window.localStorage

function valid(value: unknown, slug: string): value is PersistedCart {
  const cart = value as Partial<PersistedCart> | null
  return !!cart && (cart.version === VERSION || cart.version === 1) && cart.slug === slug &&
    Array.isArray(cart.items) && cart.items.every(item =>
      typeof item?.key === 'string' && typeof item.productId === 'string' &&
      Number.isFinite(item.quantity) && item.quantity > 0 &&
      Array.isArray(item.additionals))
}
function persist() {
  if (!state.slug) return
  storage()?.setItem(PREFIX + state.slug, JSON.stringify({
    version: VERSION, slug: state.slug,
    items: state.items.map(({ key, productId, variationId, quantity, notes, additionals }) => ({
      key, productId, variationId, quantity, notes, additionals
    }))
  }))
}
export function loadCart(slug: string) {
  state.slug = slug
  state.items = []
  const raw = storage()?.getItem(PREFIX + slug)
  if (raw) {
    try {
      const value: unknown = JSON.parse(raw)
      if (valid(value, slug)) state.items = value.items.map(item => ({
        ...item, additionals: item.additionals.map(selected => ({ groupId: null, portionNumerator: null, portionDenominator: null, ...selected })),
        productName: 'Item indisponível', variationName: null,
        displayedUnitPrice: 0
      }))
      else storage()?.removeItem(PREFIX + slug)
    } catch { storage()?.removeItem(PREFIX + slug) }
  }
  state.revision++
}
export function hydrateCartFromCatalog(catalog: PublicCatalog) {
  const products = catalog.categories.flatMap(category => category.products)
  state.items.forEach(line => {
    const product = products.find(candidate => candidate.id === line.productId)
    if (!product) return
    const variation = product.variations.find(candidate => candidate.id === line.variationId)
    const groups = product.additionalGroups
    line.additionals.forEach(selected => {
      if (!selected.groupId) selected.groupId = groups.find(group => group.items.some(option => option.id === selected.additionalId))?.id ?? null
    })
    const selections = line.additionals.map(selected => ({ selected, group: groups.find(group => group.id === selected.groupId), option: groups.flatMap(group => group.items).find(option => option.id === selected.additionalId) }))
    const composition = groups.find(group => group.pricingStrategy === 'HighestPrice' || group.pricingStrategy === 'Proportional')
    const selectedComposition = selections.filter(item => item.group?.id === composition?.id && item.option)
    const compositionPrice = composition?.pricingStrategy === 'HighestPrice'
      ? Math.max(...selectedComposition.map(item => item.option!.price), 0)
      : composition?.pricingStrategy === 'Proportional'
        ? selectedComposition.reduce((sum, item) => sum + item.option!.price * (item.selected.portionNumerator ?? 0) / (item.selected.portionDenominator ?? 1), 0)
        : variation?.price ?? product.basePrice
    line.productName = product.name + (product.isAvailable === false ? ' (indisponível)' : '')
    line.variationName = variation?.name ?? null
    line.displayedUnitPrice = compositionPrice + selections.reduce((sum, item) =>
      item.group?.pricingStrategy === 'Additive' ? sum + (item.option?.price ?? 0) * item.selected.quantity : sum, 0)
  })
}
export function usePublicCart() {
  const touch = () => { state.revision++; persist() }
  return {
    state,
    count: computed(() => state.items.reduce((sum, item) => sum + item.quantity, 0)),
    displayedTotal: computed(() => state.items.reduce(
      (sum, item) => sum + item.displayedUnitPrice * item.quantity, 0
    )),
    add(item: CartItem) { state.items.push(item); touch() },
    remove(key: string) { state.items = state.items.filter(item => item.key !== key); touch() },
    clear() { state.items = []; touch() },
    materialChange() { touch() }
  }
}
export function orderItems(items: CartItem[]): OrderItemRequest[] {
  return items.map(({ productId, variationId, quantity, notes, additionals }) => ({
    productId, variationId, quantity, notes, additionals
  }))
}
export const receiptStorage = {
  save(slug: string, reference: string) {
    storage()?.setItem('zfome.public-receipt.' + slug, reference)
  },
  read(slug: string) {
    const value = storage()?.getItem('zfome.public-receipt.' + slug) ?? ''
    return /^[0-9a-f]{48}$/.test(value) ? value : null
  },
  findSlug(reference: string) {
    const store = storage()
    if (!store) return null
    const prefix = 'zfome.public-receipt.'
    for (let index = 0; index < store.length; index++) {
      const key = store.key(index)
      if (key?.startsWith(prefix) && store.getItem(key) === reference)
        return key.slice(prefix.length)
    }
    return null
  }
}
