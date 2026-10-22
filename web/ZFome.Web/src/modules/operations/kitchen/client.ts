import { api } from '../../../http/client'
import type { KitchenTicket, KitchenTicketAction } from './types'

const root = (unit: string) =>
  '/api/admin/establishments/' + encodeURIComponent(unit)

export const kitchenDisplayClient = {
  async queue(unit: string, signal?: AbortSignal) {
    return (await api.get<KitchenTicket[]>(root(unit) + '/kitchen', { signal })).data
  },
  async execute(unit: string, orderId: string, action: KitchenTicketAction) {
    const transition = action === 'StartPreparation' ? 'prepare' : 'ready'
    await api.post(
      root(unit) + '/orders/' + encodeURIComponent(orderId) + '/' + transition,
      { note: null }
    )
  }
}
