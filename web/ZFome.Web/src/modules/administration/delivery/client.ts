import { api } from '../../../http/client'

export interface DeliveryRegion {
  id: string; name: string; postalCodeFrom: string; postalCodeTo: string
  fee: number; estimatedMinutes: number; isActive: boolean
}
export type DeliveryRegionInput = Omit<DeliveryRegion, 'id' | 'isActive'>
const path = (unit: string) => `/api/admin/establishments/${encodeURIComponent(unit)}/delivery-regions`

export const deliveryClient = {
  async list(unit: string, signal?: AbortSignal) {
    return (await api.get<DeliveryRegion[]>(path(unit), { signal })).data
  },
  async save(unit: string, id: string | null, value: DeliveryRegionInput) {
    return (await (id
      ? api.put<{ id: string }>(`${path(unit)}/${encodeURIComponent(id)}`, value)
      : api.post<{ id: string }>(path(unit), value))).data
  },
  async active(unit: string, id: string, isActive: boolean) {
    await api.patch(`${path(unit)}/${encodeURIComponent(id)}/active`, { isActive })
  }
}
