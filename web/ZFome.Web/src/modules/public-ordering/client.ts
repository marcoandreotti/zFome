import axios, { type AxiosError, type AxiosInstance } from 'axios'
import { ApiError, readCsrfCookie, type ProblemDetails } from '../../http/client'
import type { Address, Confirmation, ConfirmationRequest, PublicCatalog, PublicContext, SchedulingSlots, ServiceType, Simulation, SimulationRequest, Tracking } from './types'

const path = (slug: string) => '/api/public/ordering/' + encodeURIComponent(slug)

export function createPublicOrderingClient(http: AxiosInstance) {
  http.interceptors.request.use((config) => {
    const csrf = readCsrfCookie()
    if (csrf) config.headers.set('X-CSRF-Token', csrf)
    return config
  })
  http.interceptors.response.use(undefined, (error: AxiosError<ProblemDetails>) => {
    if (axios.isCancel(error)) throw error
    throw new ApiError({ ...error.response?.data, status: error.response?.status })
  })
  return {
  async context(slug: string, tableToken?: string, signal?: AbortSignal) {
    return (await http.get<PublicContext>(path(slug) + '/context', {
      params: tableToken ? { tableToken } : undefined, signal
    })).data
  },
  async catalog(slug: string, signal?: AbortSignal) {
    return (await http.get<PublicCatalog>(
      '/api/public/establishments/' + encodeURIComponent(slug) + '/catalog', { signal }
    )).data
  },
  async ratingSummary(slug: string, signal?: AbortSignal) {
    return (await http.get<RatingSummary | null>(`/api/public/establishments/${encodeURIComponent(slug)}/rating-summary`, { signal })).data
  },
  async scheduleSlots(slug: string, serviceType: ServiceType, signal?: AbortSignal) {
    return (await http.get<SchedulingSlots>(path(slug) + '/schedule-slots', {
      params: { serviceType }, signal
    })).data
  },
  async customer(slug: string, value: {
    name: string; phone: string; email: string | null; address: Address | null
  }, signal?: AbortSignal) {
    return (await http.post<{ customerId: string; addressId: string | null }>(
      path(slug) + '/customers', value, { signal }
    )).data
  },
  async simulate(slug: string, value: SimulationRequest, signal?: AbortSignal) {
    return (await http.post<Simulation>(path(slug) + '/simulate', value, { signal })).data
  },
  async confirm(slug: string, value: ConfirmationRequest, key: string) {
    return (await http.post<Confirmation>(path(slug) + '/orders', value, {
      headers: { 'Idempotency-Key': key }
    })).data
  },
  async track(reference: string, signal?: AbortSignal) {
    return (await http.get<Tracking>(
      '/api/public/ordering/orders/' + encodeURIComponent(reference), { signal }
    )).data
  },
  async cancel(reference: string, reason: string | null) {
    await http.post(
      '/api/public/ordering/orders/' + encodeURIComponent(reference) + '/cancel',
      { reason }
    )
  },
  async reviewProgress(reference: string, signal?: AbortSignal) {
    return (await http.get<ReviewEligibility>(`/api/public/ordering/orders/${encodeURIComponent(reference)}/reviews`, { signal })).data
  },
  async submitReviews(reference: string, value: PublishReviews, key: string) {
    return (await http.post<{ submittedAt: string; ratingsSubmitted: number }>(
      `/api/public/ordering/orders/${encodeURIComponent(reference)}/reviews`, value,
      { headers: { 'Idempotency-Key': key } }
    )).data
  }
  }
}

export interface ReviewEligibility {
  canReview: boolean; expiresAt: string | null; establishmentReviewed: boolean; pendingApproval: boolean
  establishmentName?: string; establishmentLogoUrl?: string | null; establishmentSlug?: string
  establishmentReview?: { rating: number; comment: string | null; status: string; createdAt: string } | null
  products: { productId: string; productName: string; reviewed: boolean; rating?: number | null; comment?: string | null; status?: string | null; createdAt?: string | null }[]
}
export interface PublishReviews {
  establishmentRating: number; establishmentComment: string | null
  products: { productId: string; rating: number; comment: string | null }[]
}
export interface RatingSummary { targetId: string; average: number; count: number; rating1Count: number; rating2Count: number; rating3Count: number; rating4Count: number; rating5Count: number }

export const publicOrderingClient = createPublicOrderingClient(
  axios.create({ baseURL: import.meta.env.VITE_API_BASE_URL ?? '' })
)
