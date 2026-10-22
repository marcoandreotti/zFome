import { api } from '../../../http/client'
export interface PaymentMethod {
  id: string
  code: string
  name: string
  isOnline: boolean
  allowsChange: boolean
  isActive: boolean
}
export type PaymentMethodInput = Omit<PaymentMethod, 'id' | 'isActive'>
const path = (unit: string) =>
  `/api/admin/establishments/${encodeURIComponent(unit)}/payment-methods`
export const paymentMethodsClient = {
  async search(
    unit: string,
    params: {
      search: string
      isActive?: boolean
      page: number
      pageSize: number
    },
    signal?: AbortSignal
  ) {
    return (
      await api.get<{ total: number; items: PaymentMethod[] }>(path(unit), {
        params,
        signal
      })
    ).data
  },
  async save(unit: string, id: string | null, data: PaymentMethodInput) {
    return (
      await (id
        ? api.put<{ id: string }>(
            `${path(unit)}/${encodeURIComponent(id)}`,
            data
          )
        : api.post<{ id: string }>(path(unit), data))
    ).data
  },
  async active(unit: string, id: string, isActive: boolean) {
    await api.patch(`${path(unit)}/${encodeURIComponent(id)}/active`, {
      isActive
    })
  }
}
