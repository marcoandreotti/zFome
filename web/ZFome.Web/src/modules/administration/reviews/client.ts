import { api } from '../../../http/client'

export interface ReviewRow {
  id: string; productId: string | null; productName: string | null; rating: number
  comment: string | null; status: 'Pending' | 'Published' | 'Hidden' | 'Rejected'; moderationReason: string | null
  createdAt: string; updatedAt: string
}
export interface ReviewPage { total: number; items: ReviewRow[] }
const path = (unit: string) => `/api/admin/establishments/${encodeURIComponent(unit)}/reviews`
export const reviewsClient = {
  async list(unit: string, params: { rating?: number; status?: string; from?: string; to?: string; page: number; pageSize: number }, signal?: AbortSignal) {
    return (await api.get<ReviewPage>(path(unit), { params, signal })).data
  },
  async hide(unit: string, id: string, reason: string) {
    await api.post(`${path(unit)}/${encodeURIComponent(id)}/hide`, { reason })
  },
  async reject(unit: string, id: string, reason: string) {
    await api.post(`${path(unit)}/${encodeURIComponent(id)}/reject`, { reason })
  },
  async approve(unit: string, id: string) {
    await api.post(`${path(unit)}/${encodeURIComponent(id)}/approve`)
  },
  async restore(unit: string, id: string) {
    await api.post(`${path(unit)}/${encodeURIComponent(id)}/restore`)
  }
}
