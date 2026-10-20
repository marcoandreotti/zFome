import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import PublicOrderingPage from '../src/modules/public-ordering/PublicOrderingPage.vue'
import TrackingPage from '../src/modules/public-ordering/TrackingPage.vue'
import { publicOrderingClient } from '../src/modules/public-ordering/client'
import { ApiError } from '../src/http/client'

const route = vi.hoisted(() => ({ params: { slug: 'unit', tableToken: undefined as string | undefined } }))
const push = vi.hoisted(() => vi.fn())
vi.mock('vue-router', () => ({
  useRoute: () => route,
  useRouter: () => ({ push })
}))
vi.mock('../src/modules/public-ordering/client', () => ({
  publicOrderingClient: {
    context: vi.fn(), catalog: vi.fn(), scheduleSlots: vi.fn(), customer: vi.fn(), simulate: vi.fn(),
    confirm: vi.fn(), track: vi.fn(), cancel: vi.fn()
  }
}))

const stubs = {
  QPage: { template: '<div><slot /></div>' },
  QSpinner: { template: '<span />' },
  QBtn: {
    props: ['label', 'disable', 'loading', 'type'],
    emits: ['click'],
    template: '<button :type="type || \'button\'" :disabled="disable" @click="$emit(\'click\')">{{ label }}</button>'
  },
  QDialog: {
    props: ['modelValue'], emits: ['update:modelValue'],
    template: '<div v-if="modelValue"><slot /></div>'
  },
  QCard: { template: '<section><slot /></section>' },
  QCardSection: { template: '<div><slot /></div>' },
  QCardActions: { template: '<div><slot /></div>' },
  QInput: {
    props: ['label', 'modelValue', 'type'],
    emits: ['update:modelValue'],
    template: '<label>{{ label }}<input :type="type === \'number\' ? \'number\' : \'text\'" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" /></label>'
  },
  QSelect: {
    props: ['label', 'modelValue', 'options'], emits: ['update:modelValue'],
    template: '<label>{{ label }}<select :value="modelValue" @change="$emit(\'update:modelValue\', $event.target.value)"><option v-for="item in options" :value="item.value">{{ item.label }}</option></select></label>'
  },
  QOptionGroup: {
    name: 'QOptionGroup', props: ['modelValue', 'options'], emits: ['update:modelValue'],
    template: '<div><button v-for="item in options" type="button" @click="$emit(\'update:modelValue\', item.value)">{{ item.label }}</button></div>'
  },
  QForm: { emits: ['submit'], template: '<form @submit.prevent="$emit(\'submit\')"><slot /></form>' },
  QBanner: { template: '<div><slot /></div>' }
}
const context = {
  establishmentName: 'Pizzaria', slug: 'unit',
  theme: { primaryColor: '#123456', secondaryColor: '#234567', backgroundColor: '#ffffff', textColor: '#111111', fontFamily: 'Arial', logoUrl: null },
  table: null,
  paymentMethods: [{ id: 'pay', code: 'CASH', name: 'Dinheiro', isOnline: false, allowsChange: true }]
}
const product = {
  id: 'p1', code: 'P1', name: 'Pizza', description: 'Deliciosa', basePrice: 20,
  isFeatured: false, isActive: true, allowsNotes: true, images: [],
  variations: [], additionalGroups: [{
    id: 'g1', name: 'Escolha o sabor', minimumSelection: 1, maximumSelection: 1,
    isActive: true, order: 0,
    items: [{ id: 'a1', name: 'Calabresa', price: 2, isActive: true, order: 0 }]
  }]
}
const catalog = {
  establishmentId: 'internal-not-rendered', establishmentName: 'Pizzaria', slug: 'unit',
  categories: [
    { id: 'later', parentId: null, name: 'Depois', description: null, order: 2, imageUrl: null, isActive: true, products: [] },
    { id: 'first', parentId: null, name: 'Primeiro', description: null, order: 1, imageUrl: null, isActive: true, products: [product, { ...product, id: 'off', name: 'Inativo', isActive: false }] }
  ]
}
const simulation = { subtotal: 22, discount: 0, fees: 0, total: 22, couponCode: null, items: [] }

beforeEach(() => {
  vi.clearAllMocks()
  localStorage.clear()
  route.params = { slug: 'unit', tableToken: undefined }
  vi.mocked(publicOrderingClient.context).mockResolvedValue(context)
  vi.mocked(publicOrderingClient.catalog).mockResolvedValue(catalog)
  vi.mocked(publicOrderingClient.scheduleSlots).mockResolvedValue({
    serviceType: 'Pickup', isEnabled: false, timeZoneId: 'America/Sao_Paulo',
    slotIntervalMinutes: 30, minimumAdvanceMinutes: 60, horizonDays: 30, slots: []
  })
  vi.mocked(publicOrderingClient.simulate).mockResolvedValue(simulation)
  vi.mocked(publicOrderingClient.customer).mockResolvedValue({ customerId: 'customer', addressId: null })
  vi.mocked(publicOrderingClient.confirm).mockResolvedValue({ reference: 'r'.repeat(48), number: 42, status: 'Confirmed', total: 22 })
})

describe('catálogo público', () => {
  it('comunica loading enquanto contexto e catálogo estão pendentes', () => {
    vi.mocked(publicOrderingClient.context).mockReturnValue(new Promise(() => {}))
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    expect(wrapper.get('[role="status"]').text()).toContain('Carregando cardápio')
    wrapper.unmount()
  })

  it('resolve QR, preserva ordenação e bloqueia composição inválida', async () => {
    const focus = vi.spyOn(HTMLElement.prototype, 'focus')
    route.params.tableToken = 'opaque'
    vi.mocked(publicOrderingClient.context).mockResolvedValue({
      ...context, table: { code: '10', token: 'opaque' }
    })
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    expect(publicOrderingClient.context).toHaveBeenCalledWith('unit', 'opaque', expect.any(AbortSignal))
    expect(wrapper.text().indexOf('Primeiro')).toBeLessThan(wrapper.text().indexOf('Depois'))
    expect(wrapper.text()).not.toContain('Inativo')
    await wrapper.findAll('button').find(button => button.text().includes('Pizza'))!.trigger('click')
    await wrapper.findAll('button').find(button => button.text() === 'Adicionar')!.trigger('click')
    expect(wrapper.get('[role="alert"]').text()).toContain('selecione entre 1 e 1')
    expect(focus).toHaveBeenCalled()
    await wrapper.get('input[type="checkbox"]').setValue(true)
    await wrapper.findAll('button').find(button => button.text() === 'Adicionar')!.trigger('click')
    expect(wrapper.text()).toContain('Carrinho (1)')
    focus.mockRestore()
    wrapper.unmount()
  })

  it('preserva contexto e carrinho ao buscar e trocar categoria', async () => {
    vi.mocked(publicOrderingClient.catalog).mockResolvedValue({
      ...catalog,
      categories: [
        catalog.categories[1]!,
        { ...catalog.categories[0]!, products: [{ ...product, id: 'p2', name: 'Suco' }] }
      ]
    })
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()

    await wrapper.findAll('button').find(button => button.text().includes('Pizza'))!.trigger('click')
    await wrapper.get('input[type="checkbox"]').setValue(true)
    await wrapper.findAll('button').find(button => button.text() === 'Adicionar')!.trigger('click')

    await wrapper.get('input[type="search"]').setValue('suco')
    expect(wrapper.text()).toContain('Pizzaria')
    expect(wrapper.text()).toContain('Suco')
    expect(wrapper.text()).not.toContain('PizzaDeliciosa')
    expect(wrapper.text()).toContain('Carrinho (1)')

    await wrapper.findAll('button').find(button => button.text() === 'Depois')!.trigger('click')
    expect(wrapper.text()).toContain('Pizzaria')
    expect(wrapper.text()).toContain('Suco')
    expect(wrapper.text()).toContain('Carrinho (1)')
    wrapper.unmount()
  })

  it('envia frações exatas para um grupo de composição conforme metadados', async () => {
    const composed = {
      ...product,
      additionalGroups: [{
        id: 'flavors', name: 'Sabores', minimumSelection: 1, maximumSelection: 4,
        isActive: true, order: 0, type: 'Flavor' as const, pricingStrategy: 'Proportional' as const, requiresCompleteComposition: true,
        items: [
          { id: 'calabresa', name: 'Calabresa', price: 40, isActive: true, order: 0 },
          { id: 'portuguesa', name: 'Portuguesa', price: 50, isActive: true, order: 1 }
        ]
      }]
    }
    vi.mocked(publicOrderingClient.catalog).mockResolvedValue({
      ...catalog,
      categories: catalog.categories.map(category => ({ ...category, products: [composed] }))
    })
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text().includes('Pizza'))!.trigger('click')
    const choices = wrapper.findAll('input[type="checkbox"]')
    await choices[0]!.setValue(true)
    await choices[1]!.setValue(true)
    await wrapper.findAll('button').find(button => button.text() === 'Adicionar')!.trigger('click')
    await wrapper.findAll('button').find(button => button.text().startsWith('Carrinho'))!.trigger('click')
    await flushPromises()
    const request = vi.mocked(publicOrderingClient.simulate).mock.calls.at(-1)?.[1]
    expect(request?.items[0]?.additionals).toEqual([
      { additionalId: 'calabresa', quantity: 1, groupId: 'flavors', portionNumerator: 1, portionDenominator: 2 },
      { additionalId: 'portuguesa', quantity: 1, groupId: 'flavors', portionNumerator: 1, portionDenominator: 2 }
    ])
    wrapper.unmount()
  })

  it('não revela dados quando a unidade está indisponível', async () => {
    vi.mocked(publicOrderingClient.context).mockRejectedValue(new ApiError({ status: 404 }))
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    expect(wrapper.text()).toContain('Pedidos indisponíveis')
    expect(wrapper.text()).not.toContain('internal-not-rendered')
  })

  it('orienta recuperação quando o catálogo está vazio', async () => {
    vi.mocked(publicOrderingClient.catalog).mockResolvedValue({ ...catalog, categories: [] })
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    expect(wrapper.text()).toContain('Nenhum item disponível no momento.')
    await wrapper.findAll('button').find(button => button.text() === 'Atualizar cardápio')!.trigger('click')
    await flushPromises()
    expect(publicOrderingClient.catalog).toHaveBeenCalledTimes(2)
    wrapper.unmount()
  })

  it('explica fechamento sem depender da cor', async () => {
    vi.mocked(publicOrderingClient.context).mockResolvedValue({
      ...context,
      availability: [{
        serviceType: 'Pickup', isAvailable: false, reason: 'OutsideBusinessHours',
        message: null, nextOpening: '2026-09-24T14:00:00Z'
      }]
    })
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    expect(wrapper.get('[role="status"]').text()).toContain('fora do horário de atendimento')
    expect(wrapper.text()).toContain('Próxima abertura:')
    wrapper.unmount()
  })

  it('oferece retomar a referência pública válida após recarregar', async () => {
    localStorage.setItem('orderhub.public-receipt.unit', 'a'.repeat(48))
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Retomar acompanhamento')!.trigger('click')
    expect(push).toHaveBeenCalledWith('/order/track/' + 'a'.repeat(48))
    wrapper.unmount()
  })

  it('distingue pausa operacional e bloqueia produtos temporariamente indisponíveis', async () => {
    vi.mocked(publicOrderingClient.context).mockResolvedValue({
      ...context,
      availability: [
        { serviceType: 'Pickup', isAvailable: false, reason: 'ServicePaused', message: 'Pausa para organização.', nextOpening: '2026-09-24T13:00:00Z' },
        { serviceType: 'Delivery', isAvailable: false, reason: 'OutsideBusinessHours', message: null, nextOpening: '2026-09-24T14:00:00Z' }
      ]
    })
    vi.mocked(publicOrderingClient.catalog).mockResolvedValue({
      ...catalog,
      categories: catalog.categories.map(category => ({
        ...category,
        products: category.products.map(item => item.id === 'p1'
          ? { ...item, isAvailable: false, unavailabilityReason: 'Sem estoque' }
          : item)
      }))
    })
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    expect(wrapper.text()).toContain('Pausa para organização.')
    expect(wrapper.text()).toContain('Próxima abertura:')
    const productButton = wrapper.findAll('button').find(button => button.text().includes('Pizza'))!
    expect(productButton.attributes('disabled')).toBeDefined()
    expect(productButton.text()).toContain('Indisponível')
    wrapper.unmount()
  })

  it('suprime o erro de disponibilidade quando o motivo já aparece no aviso da modalidade', async () => {
    vi.mocked(publicOrderingClient.context).mockResolvedValue({
      ...context,
      availability: [{
        serviceType: 'Pickup', isAvailable: false, reason: 'ServicePaused',
        message: 'Pausa para organização.', nextOpening: null
      }]
    })
    vi.mocked(publicOrderingClient.simulate).mockRejectedValue(new ApiError({
      status: 409, detail: 'Esta modalidade está temporariamente pausada.',
      reason: 'ServicePaused', traceId: 'availability-ref'
    }))

    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text().includes('Pizza'))!.trigger('click')
    await wrapper.get('input[type="checkbox"]').setValue(true)
    await wrapper.findAll('button').find(button => button.text() === 'Adicionar')!.trigger('click')
    await wrapper.findAll('button').find(button => button.text().startsWith('Carrinho'))!.trigger('click')
    await flushPromises()

    expect(wrapper.text().split('Pausa para organização.')).toHaveLength(2)
    expect(wrapper.text()).not.toContain('availability-ref')
    wrapper.unmount()
  })

  it('mantém visíveis erros sem relação com a disponibilidade e sua referência', async () => {
    vi.mocked(publicOrderingClient.context).mockResolvedValue({
      ...context,
      availability: [{
        serviceType: 'Pickup', isAvailable: false, reason: 'ServicePaused',
        message: 'Pausa para organização.', nextOpening: null
      }]
    })
    vi.mocked(publicOrderingClient.simulate).mockRejectedValue(new ApiError({
      status: 409, detail: 'Uma opção mudou. Revise o carrinho.',
      reason: 'OfferUnavailable', traceId: 'offer-ref'
    }))

    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text().includes('Pizza'))!.trigger('click')
    await wrapper.get('input[type="checkbox"]').setValue(true)
    await wrapper.findAll('button').find(button => button.text() === 'Adicionar')!.trigger('click')
    await wrapper.findAll('button').find(button => button.text().startsWith('Carrinho'))!.trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Uma opção mudou. Revise o carrinho.')
    expect(wrapper.text()).toContain('offer-ref')
    wrapper.unmount()
  })
})

describe('checkout idempotente', () => {
  it('confirma no slot retornado e preserva a intenção de agendamento', async () => {
    const startsAt = '2026-10-01T18:00:00Z'
    vi.mocked(publicOrderingClient.scheduleSlots).mockResolvedValue({
      serviceType: 'Pickup', isEnabled: true, timeZoneId: 'America/Sao_Paulo',
      slotIntervalMinutes: 30, minimumAdvanceMinutes: 60, horizonDays: 30,
      slots: [{ startsAt, remainingCapacity: 2 }]
    })
    localStorage.setItem('orderhub.public-cart.unit', JSON.stringify({
      version: 1, slug: 'unit', items: [{
        key: 'line', productId: 'p1', variationId: null, quantity: 1, notes: null,
        additionals: [], productName: 'Pizza', variationName: null, displayedUnitPrice: 22
      }]
    }))
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Carrinho (1)')!.trigger('click')
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Continuar')!.trigger('click')
    const groups = wrapper.findAllComponents({ name: 'QOptionGroup' })
    groups[1]!.vm.$emit('update:modelValue', 'scheduled')
    await flushPromises()
    await wrapper.findAll('select')[0]!.setValue(startsAt)
    const labels = wrapper.findAll('label')
    await labels.find(label => label.text().startsWith('Nome'))!.find('input').setValue('Ana')
    await labels.find(label => label.text().startsWith('Telefone'))!.find('input').setValue('11999999999')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(publicOrderingClient.confirm).toHaveBeenCalledWith('unit',
      expect.objectContaining({ scheduledAtUtc: startsAt }), expect.any(String))
    wrapper.unmount()
  })

  it('preserva o carrinho e pede outro horário quando o slot expira', async () => {
    const startsAt = '2026-10-01T18:00:00Z'
    vi.mocked(publicOrderingClient.scheduleSlots).mockResolvedValue({
      serviceType: 'Pickup', isEnabled: true, timeZoneId: 'America/Sao_Paulo',
      slotIntervalMinutes: 30, minimumAdvanceMinutes: 60, horizonDays: 30,
      slots: [{ startsAt, remainingCapacity: 1 }]
    })
    vi.mocked(publicOrderingClient.confirm).mockRejectedValue(new ApiError({
      status: 409, detail: 'Selected order slot has reached its capacity.'
    }))
    localStorage.setItem('orderhub.public-cart.unit', JSON.stringify({
      version: 1, slug: 'unit', items: [{
        key: 'line', productId: 'p1', variationId: null, quantity: 1, notes: null,
        additionals: [], productName: 'Pizza', variationName: null, displayedUnitPrice: 22
      }]
    }))
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Carrinho (1)')!.trigger('click')
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Continuar')!.trigger('click')
    wrapper.findAllComponents({ name: 'QOptionGroup' })[1]!.vm.$emit('update:modelValue', 'scheduled')
    await flushPromises()
    await wrapper.findAll('select')[0]!.setValue(startsAt)
    const labels = wrapper.findAll('label')
    await labels.find(label => label.text().startsWith('Nome'))!.find('input').setValue('Ana')
    await labels.find(label => label.text().startsWith('Telefone'))!.find('input').setValue('11999999999')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.text()).toContain('O carrinho foi preservado')
    expect(localStorage.getItem('orderhub.public-cart.unit')).toContain('Pizza')
    wrapper.unmount()
  })

  it('reutiliza a chave após resposta perdida e impede duplo envio', async () => {
    localStorage.setItem('orderhub.public-cart.unit', JSON.stringify({
      version: 1, slug: 'unit', items: [{
        key: 'line', productId: 'p1', variationId: null, quantity: 1, notes: null,
        additionals: [], productName: 'Pizza', variationName: null, displayedUnitPrice: 22
      }]
    }))
    vi.mocked(publicOrderingClient.confirm)
      .mockRejectedValueOnce(new Error('Resposta perdida'))
      .mockResolvedValueOnce({ reference: 'r'.repeat(48), number: 42, status: 'Confirmed', total: 22 })
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Carrinho (1)')!.trigger('click')
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Continuar')!.trigger('click')
    const labels = wrapper.findAll('label')
    await labels.find(label => label.text().startsWith('Nome'))!.find('input').setValue('Ana')
    await labels.find(label => label.text().startsWith('Telefone'))!.find('input').setValue('11999999999')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    const keys = vi.mocked(publicOrderingClient.confirm).mock.calls.map(call => call[2])
    expect(keys).toHaveLength(2)
    expect(keys[0]).toBe(keys[1])
    expect(wrapper.text()).toContain('Pedido confirmado')
    expect(localStorage.getItem('orderhub.public-receipt.unit')).toBe('r'.repeat(48))
    wrapper.unmount()
  })

  it('exige endereço completo para entrega', async () => {
    localStorage.setItem('orderhub.public-cart.unit', JSON.stringify({
      version: 1, slug: 'unit', items: [{
        key: 'line', productId: 'p1', variationId: null, quantity: 1, notes: null,
        additionals: [], productName: 'Pizza', variationName: null, displayedUnitPrice: 22
      }]
    }))
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Carrinho (1)')!.trigger('click')
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Continuar')!.trigger('click')
    wrapper.findComponent({ name: 'QOptionGroup' }).vm.$emit('update:modelValue', 'Delivery')
    await flushPromises()
    const labels = wrapper.findAll('label')
    await labels.find(label => label.text().startsWith('Nome'))!.find('input').setValue('Ana')
    await labels.find(label => label.text().startsWith('Telefone'))!.find('input').setValue('11999999999')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.get('[role="alert"]').text()).toContain('endereço de entrega')
    expect(publicOrderingClient.confirm).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('bloqueia duplo clique enquanto a confirmação está em andamento', async () => {
    localStorage.setItem('orderhub.public-cart.unit', JSON.stringify({
      version: 1, slug: 'unit', items: [{
        key: 'line', productId: 'p1', variationId: null, quantity: 1, notes: null,
        additionals: [], productName: 'Pizza', variationName: null, displayedUnitPrice: 22
      }]
    }))
    let finish!: (value: { reference: string; number: number; status: string; total: number }) => void
    vi.mocked(publicOrderingClient.confirm).mockImplementation(() =>
      new Promise(resolve => { finish = resolve }))
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Carrinho (1)')!.trigger('click')
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Continuar')!.trigger('click')
    const labels = wrapper.findAll('label')
    await labels.find(label => label.text().startsWith('Nome'))!.find('input').setValue('Ana')
    await labels.find(label => label.text().startsWith('Telefone'))!.find('input').setValue('11999999999')
    void wrapper.get('form').trigger('submit')
    void wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(publicOrderingClient.confirm).toHaveBeenCalledTimes(1)
    finish({ reference: 'r'.repeat(48), number: 42, status: 'Confirmed', total: 22 })
    await flushPromises()
    wrapper.unmount()
  })

  it('exige nova confirmação quando o total autoritativo muda', async () => {
    localStorage.setItem('orderhub.public-cart.unit', JSON.stringify({
      version: 1, slug: 'unit', items: [{
        key: 'line', productId: 'p1', variationId: null, quantity: 1, notes: null,
        additionals: [], productName: 'Pizza', variationName: null, displayedUnitPrice: 22
      }]
    }))
    vi.mocked(publicOrderingClient.simulate)
      .mockResolvedValueOnce(simulation)
      .mockResolvedValueOnce({ ...simulation, subtotal: 25, total: 25 })
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Carrinho (1)')!.trigger('click')
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Continuar')!.trigger('click')
    const labels = wrapper.findAll('label')
    await labels.find(label => label.text().startsWith('Nome'))!.find('input').setValue('Ana')
    await labels.find(label => label.text().startsWith('Telefone'))!.find('input').setValue('11999999999')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(publicOrderingClient.confirm).not.toHaveBeenCalled()
    expect(wrapper.get('[role="alert"]').text()).toContain('total mudou')
    wrapper.unmount()
  })

  it('canonicaliza telefone formatado com código do país antes de enviar', async () => {
    localStorage.setItem('orderhub.public-cart.unit', JSON.stringify({
      version: 1, slug: 'unit', items: [{
        key: 'line', productId: 'p1', variationId: null, quantity: 1, notes: null,
        additionals: [], productName: 'Pizza', variationName: null, displayedUnitPrice: 22
      }]
    }))
    const wrapper = mount(PublicOrderingPage, { global: { stubs } })
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Carrinho (1)')!.trigger('click')
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Continuar')!.trigger('click')
    const labels = wrapper.findAll('label')
    await labels.find(label => label.text().startsWith('Nome'))!.find('input').setValue('Ana')
    await labels.find(label => label.text().startsWith('Telefone'))!.find('input')
      .setValue('+55 (0) 19 99999-9999')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(publicOrderingClient.customer).toHaveBeenCalledWith('unit',
      expect.objectContaining({ phone: '19999999999' }))
    expect(publicOrderingClient.confirm).toHaveBeenCalled()
    wrapper.unmount()
  })
})

it.each([
  'O cupom não está mais elegível.',
  'Um produto não está mais disponível.'
])('mantém a intenção e explica falha autoritativa: %s', async detail => {
  localStorage.setItem('orderhub.public-cart.unit', JSON.stringify({
    version: 1, slug: 'unit', items: [{
      key: 'line', productId: 'p1', variationId: null, quantity: 1, notes: null,
      additionals: [], productName: 'Pizza', variationName: null, displayedUnitPrice: 22
    }]
  }))
  vi.mocked(publicOrderingClient.simulate).mockRejectedValue(new ApiError({ status: 409, detail }))
  const wrapper = mount(PublicOrderingPage, { global: { stubs } })
  await flushPromises()
  await wrapper.findAll('button').find(button => button.text() === 'Carrinho (1)')!.trigger('click')
  await flushPromises()
  expect(wrapper.get('[role="alert"]').text()).toContain(detail)
  expect(wrapper.text()).toContain('Pizza')
  expect(publicOrderingClient.confirm).not.toHaveBeenCalled()
  wrapper.unmount()
})

it('mantém acompanhamento após cancelamento rejeitado', async () => {
  route.params = { slug: 'unit', tableToken: undefined }
  ;(route.params as Record<string, string>).reference = 'r'.repeat(48)
  vi.mocked(publicOrderingClient.track).mockResolvedValue({
    reference: 'r'.repeat(48), number: 42, serviceType: 'Pickup', status: 'Confirmed',
    subtotal: 22, discount: 0, fees: 0, total: 22, couponCode: null, items: [],
    history: [{ status: 'Confirmed', occurredAt: '2026-09-04T12:00:00Z', note: null }]
  })
  vi.mocked(publicOrderingClient.cancel).mockRejectedValue(new ApiError({
    status: 422, detail: 'O pedido não pode mais ser cancelado.'
  }))
  const wrapper = mount(TrackingPage, { global: { stubs } })
  await flushPromises()
  await wrapper.findAll('button').find(button => button.text() === 'Cancelar pedido')!.trigger('click')
  await flushPromises()
  expect(wrapper.text()).toContain('Pedido confirmado')
  expect(wrapper.get('[role="alert"]').text()).toContain('não pode mais ser cancelado')
  wrapper.unmount()
})
