import { api } from '../../../http/client'

export type ServiceType = 'Table' | 'Pickup' | 'Delivery'
export const serviceTypeLabels: Record<ServiceType, string> = {
  Table: 'Mesa',
  Pickup: 'Retirada',
  Delivery: 'Entrega'
}
export type OfferKind = 'Product' | 'Variation' | 'Additional'
export interface ScheduleException {
  date: string
  serviceType: ServiceType | null
  isOpen: boolean
  opensAt: string | null
  closesAt: string | null
  reason: string | null
}
export interface ServicePause {
  serviceType: ServiceType
  startsAt: string
  endsAt: string | null
  reason: string | null
}
export interface AvailabilityConfiguration {
  tradeName: string
  slug: string
  timeZoneId: string
  exceptions: ScheduleException[]
  pauses: ServicePause[]
}
export interface OrderSchedulingPolicy {
  serviceType: 'Pickup' | 'Delivery'; isEnabled: boolean; slotIntervalMinutes: number
  minimumAdvanceMinutes: number; horizonDays: number; maximumOrdersPerSlot: number | null
}
export interface OrderSchedulingConfiguration { timeZoneId: string; policies: OrderSchedulingPolicy[] }
const root = (unit: string) =>
  '/api/admin/establishments/' + encodeURIComponent(unit)
export const availabilityClient = {
  async configuration(unit: string, signal?: AbortSignal) {
    return (await api.get<AvailabilityConfiguration>(root(unit) + '/configuration', { signal })).data
  },
  async scheduling(unit: string, signal?: AbortSignal) {
    return (await api.get<OrderSchedulingConfiguration>(root(unit) + '/order-scheduling', { signal })).data
  },
  async saveScheduling(unit: string, value: OrderSchedulingPolicy) {
    await api.put(root(unit) + '/order-scheduling/' + value.serviceType, {
      isEnabled: value.isEnabled, minimumAdvanceMinutes: value.minimumAdvanceMinutes,
      horizonDays: value.horizonDays, maximumOrdersPerSlot: value.maximumOrdersPerSlot
    })
  },
  async timeZone(unit: string, value: AvailabilityConfiguration) {
    await api.put(root(unit) + '/configuration', {
      tradeName: value.tradeName, slug: value.slug, timeZoneId: value.timeZoneId
    })
  },
  async exceptions(unit: string, values: ScheduleException[]) {
    await api.put(root(unit) + '/availability/exceptions', {
      exceptions: values.map(value => ({
        ...value,
        opensAt: value.opensAt && value.opensAt.length === 5 ? value.opensAt + ':00' : value.opensAt,
        closesAt: value.closesAt && value.closesAt.length === 5 ? value.closesAt + ':00' : value.closesAt
      }))
    })
  },
  async pause(unit: string, serviceType: ServiceType, endsAt: string | null, reason: string) {
    await api.post(root(unit) + '/availability/pauses', {
      serviceType, endsAt: endsAt ? new Date(endsAt).toISOString() : null,
      reason: reason.trim() || null
    })
  },
  async resume(unit: string, serviceType: ServiceType) {
    await api.delete(root(unit) + '/availability/pauses/' + serviceType)
  },
  async unavailable(unit: string, kind: OfferKind, offerId: string, endsAt: string | null, reason: string) {
    await api.put(root(unit) + '/catalog/offers/' + kind + '/' + encodeURIComponent(offerId) + '/unavailability', {
      endsAt: endsAt ? new Date(endsAt).toISOString() : null,
      reason: reason.trim() || null
    })
  },
  async reactivate(unit: string, kind: OfferKind, offerId: string) {
    await api.delete(root(unit) + '/catalog/offers/' + kind + '/' + encodeURIComponent(offerId) + '/unavailability')
  }
}
