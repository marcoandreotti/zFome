import { api } from '../../../http/client'
import type { OrderDetail, OrderFilters, OrderPage } from './types'

const path = (unit: string) =>
  '/api/admin/establishments/' + encodeURIComponent(unit) + '/orders'

export const orderOperationsClient = {
  async search(unit: string, filters: OrderFilters, signal?: AbortSignal) {
    return (
      await api.get<OrderPage>(path(unit), {
        params: { ...filters, page: 1, pageSize: 100 },
        signal
      })
    ).data
  },
  async detail(unit: string, id: string, signal?: AbortSignal) {
    return (
      await api.get<OrderDetail>(
        path(unit) + '/' + encodeURIComponent(id),
        { signal }
      )
    ).data
  },
  async transition(
    unit: string,
    id: string,
    transition: OrderTransition,
    note?: string
  ) {
    await api.post(
      path(unit) + '/' + encodeURIComponent(id) + '/' + transition,
      { note: note?.trim() || null }
    )
  }
}

export type OrderTransition =
  | 'prepare'
  | 'ready'
  | 'dispatch'
  | 'complete'
  | 'cancel'
  | 'reject'
