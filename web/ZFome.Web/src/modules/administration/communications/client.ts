import { api } from '../../../http/client'

export type NotificationChannel = 'Email' | 'WhatsApp'
export interface NotificationTemplate {
  id: string
  purpose: string
  channel: NotificationChannel
  language: string
  subject: string
  body: string
  providerTemplateName: string | null
  requiresConsent: boolean
  isActive: boolean
  updatedAtUtc: string
}
export interface NotificationConsent {
  id: string
  channel: NotificationChannel
  purpose: string
  destination: string
  isGranted: boolean
  capturedAtUtc: string
  source: string | null
}
export interface NotificationHistory {
  id: string
  channel: NotificationChannel
  purpose: string
  destination: string
  status: string
  providerMessageId: string | null
  attemptCount: number
  lastError: string | null
  createdAtUtc: string
  updatedAtUtc: string | null
}
export interface NotificationAttempt {
  attemptNumber: number
  status: string
  providerMessageId: string | null
  safeErrorCode: string | null
  createdAtUtc: string
}
export interface NotificationTemplateInput extends Omit<NotificationTemplate, 'id' | 'updatedAtUtc'> {
  id?: string | null
}
const path = (unit: string) =>
  `/api/admin/establishments/${encodeURIComponent(unit)}/communications`

export const communicationsClient = {
  async templates(unit: string, signal?: AbortSignal) {
    return (await api.get<NotificationTemplate[]>(`${path(unit)}/templates`, { signal })).data
  },
  async saveTemplate(unit: string, template: NotificationTemplateInput) {
    return (
      await api.put<{ id: string }>(`${path(unit)}/templates`, template)
    ).data
  },
  async consents(unit: string, signal?: AbortSignal) {
    return (await api.get<NotificationConsent[]>(`${path(unit)}/consents`, { signal })).data
  },
  async saveConsent(
    unit: string,
    consent: {
      channel: NotificationChannel
      purpose: string
      destination: string
      isGranted: boolean
      source: string
    }
  ) {
    await api.put(`${path(unit)}/consents`, consent)
  },
  async request(
    unit: string,
    data: {
      templateId: string
      channel: NotificationChannel
      destination: string
      parameters: Record<string, string>
      idempotencyKey: string
    }
  ) {
    return (
      await api.post<{ id: string }>(`${path(unit)}/notifications`, data)
    ).data
  },
  async history(unit: string, filters: { fromUtc?: string; toUtcExclusive?: string; signal?: AbortSignal } = {}) {
    return (
      await api.get<NotificationHistory[]>(`${path(unit)}/notifications`, {
        params: { page: 1, pageSize: 100, fromUtc: filters.fromUtc, toUtcExclusive: filters.toUtcExclusive },
        signal: filters.signal
      })
    ).data
  },
  async attempts(unit: string, notificationId: string) {
    return (
      await api.get<NotificationAttempt[]>(
        `${path(unit)}/notifications/${encodeURIComponent(notificationId)}/attempts`
      )
    ).data
  }
}
