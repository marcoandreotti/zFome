import { api } from '../../../http/client'
export interface Coupon {
  id: string
  code: string
  description: string | null
  discountType: 'Percentage' | 'FixedAmount'
  value: number
  minimumOrder: number
  startsAt: string
  endsAt: string
  maximumUses: number | null
  usedCount: number
  isActive: boolean
}
export type CouponInput = Omit<Coupon, 'id' | 'usedCount' | 'isActive'>
const path = (unit: string) =>
  `/api/admin/establishments/${encodeURIComponent(unit)}/coupons`
export const couponsClient = {
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
      await api.get<{ total: number; items: Coupon[] }>(path(unit), {
        params,
        signal
      })
    ).data
  },
  async save(unit: string, id: string | null, data: CouponInput) {
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
// datetime-local não contém fuso: converter explicitamente nos limites do contrato HTTP.
export function localDateTime(instant: string): string {
  const date = new Date(instant)
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000)
    .toISOString()
    .slice(0, 19)
}
export function couponPayload(
  coupon: Coupon,
  startsAt: string,
  endsAt: string
): CouponInput {
  return {
    code: coupon.code,
    description: coupon.description || null,
    discountType: coupon.discountType,
    value: Number(coupon.value),
    minimumOrder: Number(coupon.minimumOrder),
    startsAt: new Date(startsAt).toISOString(),
    endsAt: new Date(endsAt).toISOString(),
    maximumUses:
      coupon.maximumUses === null || String(coupon.maximumUses) === ''
        ? null
        : Number(coupon.maximumUses)
  }
}
