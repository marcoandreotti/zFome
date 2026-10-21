import type { OrderSummary } from './types'

export const sortImmediateOrders = (orders: OrderSummary[]) => [...orders]
  .sort((first, second) => Date.parse(second.createdAt) - Date.parse(first.createdAt))

export const sortScheduledOrders = (orders: OrderSummary[]) => [...orders]
  .sort((first, second) =>
    Date.parse(first.scheduledAtUtc ?? first.createdAt) -
    Date.parse(second.scheduledAtUtc ?? second.createdAt))

export const isProductionDue = (order: OrderSummary, now = Date.now()) =>
  !!order.scheduledAtUtc && Date.parse(order.scheduledAtUtc) <= now
