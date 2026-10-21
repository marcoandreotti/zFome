import type { OrderTransition } from './client'
import type { OrderDetail, OrderServiceType, OrderStatus } from './types'

export interface OrderAction {
  transition: OrderTransition
  label: string
  capability:
    | 'order-attendance'
    | 'order-kitchen'
    | 'order-delivery'
    | 'order-completion'
  destructive?: boolean
}

const actions: Record<OrderStatus, OrderAction[]> = {
  Confirmed: [
    { transition: 'prepare', label: 'Iniciar preparo', capability: 'order-kitchen' },
    { transition: 'reject', label: 'Rejeitar', capability: 'order-attendance', destructive: true },
    { transition: 'cancel', label: 'Cancelar', capability: 'order-attendance', destructive: true }
  ],
  Preparing: [
    { transition: 'ready', label: 'Marcar pronto', capability: 'order-kitchen' },
    { transition: 'cancel', label: 'Cancelar', capability: 'order-attendance', destructive: true }
  ],
  Ready: [
    { transition: 'dispatch', label: 'Saiu para entrega', capability: 'order-delivery' },
    { transition: 'complete', label: 'Concluir', capability: 'order-completion' }
  ],
  OutForDelivery: [
    { transition: 'complete', label: 'Concluir entrega', capability: 'order-completion' }
  ],
  Completed: [],
  Cancelled: [],
  Rejected: []
}

export function availableActions(
  order: Pick<OrderDetail, 'status' | 'serviceType'>,
  capabilities: readonly string[]
) {
  return actions[order.status].filter((action) => {
    if (!capabilities.includes(action.capability)) return false
    if (order.status !== 'Ready') return true
    return action.transition === 'dispatch'
      ? order.serviceType === 'Delivery'
      : order.serviceType !== 'Delivery'
  })
}

export const statusLabels: Record<OrderStatus, string> = {
  Confirmed: 'Confirmados',
  Preparing: 'Em preparo',
  Ready: 'Prontos',
  OutForDelivery: 'Em entrega',
  Completed: 'Concluídos',
  Cancelled: 'Cancelados',
  Rejected: 'Rejeitados'
}

export const orderStatusTone: Record<OrderStatus, string> = {
  Confirmed: 'confirmed',
  Preparing: 'preparing',
  Ready: 'ready',
  OutForDelivery: 'info',
  Completed: 'success',
  Cancelled: 'danger',
  Rejected: 'danger'
}

export const serviceLabels: Record<OrderServiceType, string> = {
  Table: 'Mesa',
  Pickup: 'Retirada',
  Delivery: 'Entrega'
}
