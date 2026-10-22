import { api } from '../../../http/client'
export interface Theme { primaryColor: string | null; secondaryColor: string | null; backgroundColor: string | null; textColor: string | null; fontFamily: string | null; logoUrl: string | null; faviconUrl: string | null }
export interface Hours { dayOfWeek: number; opensAt: string; closesAt: string }
export interface Configuration { tradeName: string; slug: string; theme: Theme; hours: Hours[] }
export interface Progress { dataReady: boolean; themeReady: boolean; hoursReady: boolean; accessReady: boolean; activeTables: number; completedAt: string | null; isReady: boolean; pendingSteps: string[] }
export interface Table { id: string; code: string; description: string | null; isActive: boolean; publicPath: string | null }
export interface TablePage { items: Table[]; totalCount: number; page: number; pageSize: number }
export interface TableIntent { intentId: string; code: string; description: string }
const root = (id: string) => `/api/admin/establishments/${encodeURIComponent(id)}`
export const onboardingClient = {
  async progress(id: string, signal?: AbortSignal) { return (await api.get<Progress>(`${root(id)}/onboarding`, { signal })).data },
  async configuration(id: string, signal?: AbortSignal) { return (await api.get<Configuration>(`${root(id)}/configuration`, { signal })).data },
  async data(id: string, tradeName: string, slug: string) { await api.put(`${root(id)}/configuration`, { tradeName, slug }) },
  async theme(id: string, theme: Theme) { await api.put(`${root(id)}/theme`, theme) },
  async hours(id: string, hours: Hours[]) { await api.put(`${root(id)}/business-hours`, { hours: hours.map(h => ({ ...h, opensAt: h.opensAt.length === 5 ? `${h.opensAt}:00` : h.opensAt, closesAt: h.closesAt.length === 5 ? `${h.closesAt}:00` : h.closesAt })) }) },
  async tables(id: string, page = 1, signal?: AbortSignal) { return (await api.get<TablePage>(`${root(id)}/tables`, { params: { page, pageSize: 20 }, signal })).data },
  async createTable(id: string, request: TableIntent) { return (await api.post<{ id: string }>(`${root(id)}/tables`, request)).data },
  async updateTable(id: string, table: Table) { await api.put(`${root(id)}/tables/${table.id}`, { code: table.code, description: table.description, isActive: table.isActive }) },
  async rotate(id: string, tableId: string) { await api.post(`${root(id)}/tables/${tableId}/rotate-token`) },
  async complete(id: string) { await api.post(`${root(id)}/onboarding/complete`) }
}
export function tableIntent(unitId: string): TableIntent {
  const saved = sessionStorage.getItem(`onboarding-table:${unitId}`)
  if (saved) { try { return JSON.parse(saved) as TableIntent } catch { /* start a new draft */ } }
  return { intentId: crypto.randomUUID(), code: '', description: '' }
}
export function publicTableUrl(path: string) { return new URL(path, window.location.origin).href }

