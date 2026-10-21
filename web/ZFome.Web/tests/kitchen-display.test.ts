import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mount } from '@vue/test-utils'
import { ApiError } from '../src/http/client'
import { kitchenDisplayClient } from '../src/modules/operations/kitchen/client'
import { useKitchenDisplayStore } from '../src/modules/operations/kitchen/store'
import type { KitchenTicket } from '../src/modules/operations/kitchen/types'
import KitchenDisplayPage from '../src/modules/operations/kitchen/KitchenDisplayPage.vue'
import KitchenTicketCard from '../src/modules/operations/kitchen/KitchenTicketCard.vue'
import { useSessionStore } from '../src/modules/session/store'

vi.mock('../src/modules/operations/orders/realtime', () => ({
  createOrderRealtimeConnection: () => ({}),
  OrderRealtimeCoordinator: class {
    start() { return Promise.resolve() }
    stop() { return Promise.resolve() }
  }
}))

const ticket = (overrides: Partial<KitchenTicket> = {}): KitchenTicket => ({
  id: 'order-1',
  number: 42,
  serviceType: 'Table',
  status: 'Confirmed',
  customerName: 'Maria',
  tableCode: 'A1',
  confirmedAt: '2026-09-28T12:00:00Z',
  preparationStartedAt: null,
  action: 'StartPreparation',
  items: [{
    id: 'item-1',
    productName: 'Hambúrguer',
    variationName: 'Duplo',
    quantity: 2,
    notes: 'Sem cebola',
    additionals: [{ name: 'Molho', quantity: 1 }]
  }],
  ...overrides
})

beforeEach(() => {
  setActivePinia(createPinia())
  vi.restoreAllMocks()
})

describe('fila KDS', () => {
  it('remove o snapshot anterior antes de carregar outra unidade', async () => {
    let resolve!: (value: KitchenTicket[]) => void
    vi.spyOn(kitchenDisplayClient, 'queue')
      .mockResolvedValueOnce([ticket()])
      .mockImplementationOnce(() => new Promise((done) => { resolve = done }))
    const store = useKitchenDisplayStore()
    await store.synchronize('unit-a')

    const loading = store.synchronize('unit-b')

    expect(store.unitId).toBe('unit-b')
    expect(store.tickets).toEqual([])
    resolve([ticket({ id: 'order-2' })])
    await loading
    expect(store.tickets.map((value) => value.id)).toEqual(['order-2'])
  })

  it('reconcilia a fila quando outro terminal vence a transição', async () => {
    vi.spyOn(kitchenDisplayClient, 'execute').mockRejectedValue(
      new ApiError({ status: 409, detail: 'Estado alterado.' })
    )
    vi.spyOn(kitchenDisplayClient, 'queue').mockResolvedValue([])
    const store = useKitchenDisplayStore()
    store.unitId = 'unit'
    store.tickets = [ticket()]

    await expect(store.execute('unit', store.tickets[0]!)).rejects.toMatchObject({
      problem: { status: 409 }
    })

    expect(store.tickets).toEqual([])
    expect(store.conflict).toContain('Outro terminal')
  })

  it('mantém o snapshot visível e sinaliza falha temporária', async () => {
    vi.spyOn(kitchenDisplayClient, 'queue')
      .mockResolvedValueOnce([ticket()])
      .mockRejectedValueOnce(new Error('offline'))
    const store = useKitchenDisplayStore()
    await store.synchronize('unit')
    await expect(store.synchronize('unit')).rejects.toThrow('offline')
    expect(store.tickets).toHaveLength(1)
    expect(store.stale).toBe(true)
  })
})

describe('painel KDS acessível', () => {
  it('mantém número, tempo, quantidades, modificadores e observações no ticket da feature', async () => {
    const wrapper = mount(KitchenTicketCard, {
      props: { ticket: ticket(), now: Date.parse('2026-09-28T12:16:00Z'), busyId: null },
      global: {
        stubs: {
          QBtn: {
            props: ['label'], emits: ['click'],
            template: '<button type="button" @click="$emit(\'click\')">{{ label }}</button>'
          }
        }
      }
    })
    expect(wrapper.text()).toContain('#42')
    expect(wrapper.text()).toContain('16 min')
    expect(wrapper.text()).toContain('2× Hambúrguer — Duplo')
    expect(wrapper.text()).toContain('1× Molho')
    expect(wrapper.text()).toContain('Sem cebola')
    expect(wrapper.text()).toContain('Aguardando preparo')
    expect(wrapper.get('article').attributes('tabindex')).toBe('0')
    await wrapper.get('button').trigger('click')
    expect(wrapper.emitted('advance')?.[0]?.[0]).toMatchObject({ id: 'order-1' })
  })

  it('distingue prioridade, atraso e ação de conclusão sem depender de cor', () => {
    const wrapper = mount(KitchenTicketCard, {
      props: {
        ticket: ticket({ status: 'Preparing', action: 'MarkReady', preparationStartedAt: '2026-09-28T12:05:00Z' }),
        now: Date.parse('2026-09-28T12:20:00Z'), busyId: null
      },
      global: { stubs: { QBtn: { props: ['label'], template: '<button type="button">{{ label }}</button>' } } }
    })
    expect(wrapper.text()).toContain('▶ Prioridade: em preparo')
    expect(wrapper.text()).toContain('⚠ Pedido atrasado')
    expect(wrapper.get('.ticket-action').text()).toBe('Marcar pronto')
    expect(wrapper.get('.kitchen-ticket').classes()).toContain('late')
  })

  it('não expõe fila sem uma unidade autorizada selecionada', () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const session = useSessionStore()
    session.context = {
      passwordChangeRequired: false, isPlatformUser: false,
      capabilities: ['order-read', 'order-kitchen'], establishments: []
    }
    const passthrough = { template: '<div><slot /><slot name="action" /></div>' }
    const wrapper = mount(KitchenDisplayPage, {
      global: {
        plugins: [pinia],
        stubs: { QPage: passthrough, QBanner: passthrough, QBtn: { props: ['label'], template: '<button>{{ label }}</button>' } }
      }
    })
    expect(wrapper.text()).toContain('Selecione uma unidade autorizada')
    expect(wrapper.find('[aria-label="Fila da cozinha"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('exibe preparo completo e usa a ação autorizada pelo servidor', () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const session = useSessionStore()
    session.context = {
      passwordChangeRequired: false,
      isPlatformUser: false,
      capabilities: ['order-read', 'order-kitchen'],
      establishments: [{ id: 'unit', name: 'Unidade' }]
    }
    session.selectUnit('unit')
    const store = useKitchenDisplayStore()
    store.unitId = 'unit'
    store.tickets = [ticket()]
    const passthrough = { template: '<div><slot /><slot name="action" /></div>' }

    const wrapper = mount(KitchenDisplayPage, {
      global: {
        plugins: [pinia],
        stubs: {
          QPage: passthrough,
          QBanner: passthrough,
          QBadge: { props: ['label'], template: '<span>{{ label }}</span>' },
          QBtn: { props: ['label'], template: '<button type="button">{{ label }}</button>' }
        }
      }
    })

    expect(wrapper.text()).toContain('2× Hambúrguer')
    expect(wrapper.text()).toContain('1× Molho')
    expect(wrapper.text()).toContain('Sem cebola')
    expect(wrapper.get('.ticket-action').text()).toBe('Iniciar preparo')
    expect(wrapper.get('section[aria-label="Fila da cozinha"]')).toBeTruthy()
    wrapper.unmount()
  })
})
