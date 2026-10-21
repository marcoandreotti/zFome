import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { mount, flushPromises } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { ApiError } from '../src/http/client'
import { availableActions } from '../src/modules/operations/orders/actions'
import { orderOperationsClient } from '../src/modules/operations/orders/client'
import { PollingCoordinator } from '../src/modules/operations/orders/polling'
import {
  OrderRealtimeCoordinator,
  type OrderUpdatedMessageV1,
  type RealtimeConnection
} from '../src/modules/operations/orders/realtime'
import { useOrderOperationsStore } from '../src/modules/operations/orders/store'
import type { OrderDetail, OrderSummary } from '../src/modules/operations/orders/types'
import OrdersDashboardPage from '../src/modules/operations/orders/OrdersDashboardPage.vue'
import OperationsOrderCard from '../src/modules/operations/orders/OperationsOrderCard.vue'
import OperationsSyncStatus from '../src/modules/operations/orders/OperationsSyncStatus.vue'
import { isProductionDue, sortImmediateOrders, sortScheduledOrders } from '../src/modules/operations/orders/board'
import { useSessionStore } from '../src/modules/session/store'

const summary = (id: string, status: OrderSummary['status'] = 'Confirmed'): OrderSummary => ({
  id,
  number: Number(id),
  serviceType: 'Delivery',
  status,
  customerName: 'Cliente',
  customerPhone: null,
  total: 42,
  createdAt: '2026-09-14T10:00:00Z',
  items: [{ quantity: 2, productName: 'Pizza', variationName: 'Grande' }]
})
const detail = (status: OrderDetail['status'] = 'Confirmed'): OrderDetail => ({
  ...summary('1', status),
  number: 1,
  publicReference: null,
  tableCode: null,
  subtotal: 42,
  discount: 0,
  fees: 0,
  couponCode: null,
  confirmedAmount: 0,
  isFullyPaid: false,
  items: [],
  history: []
})

beforeEach(() => {
  setActivePinia(createPinia())
  vi.restoreAllMocks()
})

describe('store operacional normalizada', () => {
  it('atualiza incrementalmente sem duplicar e elimina dados ao trocar de unidade', async () => {
    vi.spyOn(orderOperationsClient, 'search')
      .mockResolvedValueOnce({ page: 1, pageSize: 100, total: 1, items: [summary('1')] })
      .mockResolvedValueOnce({ page: 1, pageSize: 100, total: 2, items: [summary('1', 'Preparing'), summary('2')] })
    const store = useOrderOperationsStore()
    await store.synchronize('unit-a', {})
    await store.synchronize('unit-a', {})
    expect(store.ids).toEqual(['1', '2'])
    expect(store.summaries['1']?.status).toBe('Preparing')
    expect(store.isNew('2')).toBe(true)
    store.reset('unit-b')
    expect(store.ids).toEqual([])
    expect(store.details).toEqual({})
  })

  it('mantém o último snapshot, marca falha e recupera na consulta seguinte', async () => {
    vi.spyOn(orderOperationsClient, 'search')
      .mockResolvedValueOnce({ page: 1, pageSize: 100, total: 1, items: [summary('1')] })
      .mockRejectedValueOnce(new Error('API indisponível'))
      .mockResolvedValueOnce({ page: 1, pageSize: 100, total: 1, items: [summary('1', 'Preparing')] })
    const store = useOrderOperationsStore()
    await store.synchronize('unit', {})
    const lastSuccess = store.lastSuccessAt
    await expect(store.synchronize('unit', {})).rejects.toThrow('API indisponível')
    expect(store.stale).toBe(true)
    expect(store.lastSuccessAt).toBe(lastSuccess)
    expect(store.ids).toEqual(['1'])
    await store.synchronize('unit', {})
    expect(store.stale).toBe(false)
    expect(store.summaries['1']?.status).toBe('Preparing')
  })

  it('recarrega a fonte autoritativa e explica conflito concorrente', async () => {
    vi.spyOn(orderOperationsClient, 'transition').mockRejectedValue(
      new ApiError({ status: 409, detail: 'Estado alterado.' })
    )
    vi.spyOn(orderOperationsClient, 'detail').mockResolvedValue(detail('Preparing'))
    const store = useOrderOperationsStore()
    await expect(store.transition('unit', '1', 'prepare', {})).rejects.toMatchObject({
      problem: { status: 409 }
    })
    expect(store.details['1']?.status).toBe('Preparing')
    expect(store.conflict).toContain('Outro operador')
  })

  it('mantém proibição do servidor visível sem atualizar o estado otimisticamente', async () => {
    vi.spyOn(orderOperationsClient, 'transition').mockRejectedValue(
      new ApiError({ status: 403, detail: 'Ação não permitida.' })
    )
    const store = useOrderOperationsStore()
    store.details['1'] = detail()
    await expect(store.transition('unit', '1', 'prepare', {})).rejects.toMatchObject({
      problem: { status: 403 }
    })
    expect(store.details['1']?.status).toBe('Confirmed')
    expect(store.actionError).toBe('Ação não permitida.')
  })
})

describe('matriz visual de ações', () => {
  it('oferece somente ações compatíveis com capacidade, estado e atendimento', () => {
    expect(availableActions(detail(), ['order-kitchen']).map((x) => x.transition)).toEqual(['prepare'])
    expect(availableActions(detail('Preparing'), ['order-attendance']).map((x) => x.transition)).toEqual(['cancel'])
    expect(availableActions(detail('Ready'), ['order-delivery']).map((x) => x.transition)).toEqual(['dispatch'])
    expect(
      availableActions({ ...detail('Ready'), serviceType: 'Pickup' }, ['order-completion']).map((x) => x.transition)
    ).toEqual(['complete'])
  })

  it('apresenta itens essenciais e somente a próxima ação autorizada no cartão', async () => {
    const action = availableActions(detail(), ['order-kitchen'])[0]!
    const wrapper = mount(OperationsOrderCard, {
      props: {
        order: summary('1'), statusLabel: 'Confirmados', serviceLabel: 'Entrega',
        timeLabel: 'há 5 min', totalLabel: 'R$ 42,00', selected: false,
        isNew: true, isLate: false, lateLabel: 'Atrasado', nextAction: action
      },
      global: {
        stubs: {
          QBtn: {
            props: ['label'], emits: ['click'],
            template: '<button type="button" @click="$emit(\'click\')">{{ label }}</button>'
          }
        }
      }
    })
    expect(wrapper.text()).toContain('2× Pizza · Grande')
    expect(wrapper.text()).toContain('Iniciar preparo')
    await wrapper.findAll('button').find(button => button.text() === 'Iniciar preparo')!.trigger('click')
    expect(wrapper.emitted('action')?.[0]).toEqual([expect.objectContaining({ id: '1' }), action])
  })

  it('comunica atraso por texto e ícone e mantém controles nativos de teclado', () => {
    const wrapper = mount(OperationsOrderCard, {
      props: {
        order: summary('7'), statusLabel: 'Confirmados', serviceLabel: 'Entrega',
        timeLabel: 'há 18 min', totalLabel: 'R$ 42,00', selected: false,
        isNew: false, isLate: true, lateLabel: 'Atrasado'
      }
    })
    expect(wrapper.get('.order-card-shell').classes()).toContain('late')
    expect(wrapper.text()).toContain('⚠ Atrasado')
    expect(wrapper.get('button.order-card').attributes()).toMatchObject({
      type: 'button',
      'aria-label': 'Pedido 7, Confirmados, Entrega, há 18 min'
    })
  })
})

describe('organização temporal do board', () => {
  it('ordena imediatos pelos mais recentes e agendados pelo horário prometido', () => {
    const older = { ...summary('1'), createdAt: '2026-09-29T10:00:00Z' }
    const newer = { ...summary('2'), createdAt: '2026-09-29T11:00:00Z' }
    const later = { ...summary('3'), scheduledAtUtc: '2026-09-29T15:00:00Z' }
    const sooner = { ...summary('4'), scheduledAtUtc: '2026-09-29T14:00:00Z' }
    expect(sortImmediateOrders([older, newer]).map(order => order.id)).toEqual(['2', '1'])
    expect(sortScheduledOrders([later, sooner]).map(order => order.id)).toEqual(['4', '3'])
  })

  it('promove visualmente o agendado quando chega a janela de produção', () => {
    const scheduled = { ...summary('5'), scheduledAtUtc: '2026-09-29T14:00:00Z' }
    expect(isProductionDue(scheduled, Date.parse('2026-09-29T14:00:00Z'))).toBe(true)
    expect(isProductionDue(scheduled, Date.parse('2026-09-29T13:59:59Z'))).toBe(false)
  })
})

describe('polling controlado', () => {
  it('não sobrepõe ciclos e agenda o próximo somente após concluir', async () => {
    vi.useFakeTimers()
    let finish!: () => void
    const run = vi.fn(() => new Promise<void>((resolve) => { finish = resolve }))
    const poller = new PollingCoordinator(run, { intervalMs: 1000, maxIntervalMs: 8000 })
    poller.start()
    expect(run).toHaveBeenCalledOnce()
    await vi.advanceTimersByTimeAsync(5000)
    expect(run).toHaveBeenCalledOnce()
    finish()
    await Promise.resolve()
    await vi.advanceTimersByTimeAsync(1000)
    expect(run).toHaveBeenCalledTimes(2)
    poller.stop()
    vi.useRealTimers()
  })

  it('pausa oculta, permite atualização manual e limita backoff', async () => {
    vi.useFakeTimers()
    const listeners = new Set<EventListenerOrEventListenerObject>()
    const visibility = {
      hidden: true,
      addEventListener: (_: string, value: EventListenerOrEventListenerObject) => listeners.add(value),
      removeEventListener: (_: string, value: EventListenerOrEventListenerObject) => listeners.delete(value)
    }
    const run = vi.fn().mockRejectedValue(new Error('offline'))
    const poller = new PollingCoordinator(run, {
      intervalMs: 1000,
      maxIntervalMs: 2000,
      visibility: visibility as unknown as Document
    })
    poller.start()
    expect(run).not.toHaveBeenCalled()
    await poller.refresh(true)
    expect(run).toHaveBeenCalledOnce()
    visibility.hidden = false
    for (const listener of listeners)
      if (typeof listener === 'function') listener(new Event('visibilitychange'))
    await Promise.resolve()
    expect(run).toHaveBeenCalledTimes(2)
    await vi.advanceTimersByTimeAsync(2000)
    expect(run).toHaveBeenCalledTimes(3)
    poller.stop()
    vi.useRealTimers()
  })
})

describe('tempo real com reconciliação', () => {
  it('reconcilia antes de declarar a reconexão sincronizada', async () => {
    const connection = new FakeRealtimeConnection()
    let finish!: () => void
    const refresh = vi.fn(() => Promise.resolve())
    const fallback = {
      start: vi.fn(),
      stop: vi.fn(),
      refresh
    }
    const states: string[] = []
    const realtime = new OrderRealtimeCoordinator(
      connection,
      fallback,
      (state) => states.push(state)
    )

    await realtime.start('unit-a')
    expect(connection.invocations).toEqual([['SubscribeAsync', 'unit-a']])
    connection.reconnecting()
    refresh.mockImplementationOnce(() => new Promise<void>((resolve) => { finish = resolve }))
    connection.reconnected()
    await Promise.resolve()

    expect(states.at(-1)).toBe('reconnecting')
    finish()
    await flushPromises()
    expect(states.at(-1)).toBe('connected')
    expect(fallback.stop).toHaveBeenCalled()
  })

  it('reage somente a eventos v1 da unidade atual e usa a reconciliação sem sobrepor polling', async () => {
    const connection = new FakeRealtimeConnection()
    const fallback = {
      start: vi.fn(),
      stop: vi.fn(),
      refresh: vi.fn().mockResolvedValue(undefined)
    }
    const realtime = new OrderRealtimeCoordinator(connection, fallback, vi.fn())
    await realtime.start('unit-a')
    fallback.refresh.mockClear()

    connection.message({
      version: 1,
      orderId: '1',
      changeType: 'StatusChanged',
      establishmentId: 'unit-b',
      occurredAt: '2026-09-25T12:00:00Z'
    })
    connection.message({
      version: 1,
      orderId: '1',
      changeType: 'StatusChanged',
      establishmentId: 'unit-a',
      occurredAt: '2026-09-25T12:00:00Z'
    })

    expect(fallback.refresh).toHaveBeenCalledOnce()
  })
})

describe('estado de sincronização', () => {
  it('mantém fallback, defasagem, última sincronização e recuperação na mesma hierarquia', async () => {
    const passthrough = { template: '<div><slot /><slot name="action" /></div>' }
    const wrapper = mount(OperationsSyncStatus, {
      props: {
        hasUnit: true, realtimeState: 'reconnecting', stale: true,
        error: 'API temporariamente indisponível.', lastSuccessLabel: '14:30'
      },
      global: {
        stubs: {
          QBanner: passthrough,
          QBtn: {
            props: ['label'], emits: ['click'],
            template: '<button type="button" @click="$emit(\'click\')">{{ label }}</button>'
          }
        }
      }
    })
    expect(wrapper.text()).toContain('Dados possivelmente desatualizados')
    expect(wrapper.text()).toContain('A fila carregada foi preservada')
    expect(wrapper.text()).toContain('Última sincronização: 14:30')
    await wrapper.get('button').trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
  })
})

describe('componente acessível', () => {
  it('usa cartão acionável por teclado e move o foco para o detalhe aberto', async () => {
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
    vi.spyOn(orderOperationsClient, 'search').mockResolvedValue({
      page: 1,
      pageSize: 100,
      total: 1,
      items: [summary('1')]
    })
    vi.spyOn(orderOperationsClient, 'detail').mockResolvedValue(detail())
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [{ path: '/', component: OrdersDashboardPage }]
    })
    await router.push('/')
    await router.isReady()
    const passthrough = { template: '<div><slot /><slot name="action" /></div>' }
    const wrapper = mount(OrdersDashboardPage, {
      attachTo: document.body,
      global: {
        plugins: [pinia, router],
        stubs: {
          QPage: passthrough,
          QBanner: passthrough,
          QSelect: passthrough,
          QInput: passthrough,
          QBadge: { props: ['label'], template: '<span>{{ label }}</span>' },
          QBtn: { template: '<button type="button"><slot /></button>' },
          QDialog: passthrough,
          QCard: passthrough,
          QCardSection: passthrough,
          QCardActions: passthrough
        }
      }
    })
    await flushPromises()
    const card = wrapper.get('button.order-card')
    expect(card.attributes('type')).toBe('button')
    await card.trigger('click')
    await flushPromises()
    const panel = wrapper.get('[aria-label="Detalhes do pedido"]')
    expect(panel.attributes('tabindex')).toBe('-1')
    expect(document.activeElement).toBe(panel.element)
    wrapper.unmount()
  })
})

class FakeRealtimeConnection implements RealtimeConnection {
  readonly invocations: unknown[][] = []
  private messageHandler: (message: OrderUpdatedMessageV1) => void = () => undefined
  private reconnectingHandler: () => void = () => undefined
  private reconnectedHandler: () => void = () => undefined
  private closeHandler: () => void = () => undefined

  start() { return Promise.resolve() }
  stop() { return Promise.resolve() }
  invoke(methodName: string, ...args: unknown[]) {
    this.invocations.push([methodName, ...args])
    return Promise.resolve()
  }
  on(_methodName: string, handler: (message: OrderUpdatedMessageV1) => void) {
    this.messageHandler = handler
  }
  onreconnecting(handler: () => void) { this.reconnectingHandler = handler }
  onreconnected(handler: () => void) { this.reconnectedHandler = handler }
  onclose(handler: () => void) { this.closeHandler = handler }
  message(value: OrderUpdatedMessageV1) { this.messageHandler(value) }
  reconnecting() { this.reconnectingHandler() }
  reconnected() { this.reconnectedHandler() }
  close() { this.closeHandler() }
}
