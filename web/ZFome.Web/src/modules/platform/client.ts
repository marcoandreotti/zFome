import { api } from '../../http/client'

export interface PlatformUnit { id: string; name: string; slug: string; isActive: boolean; onboardingCompleted: boolean }
export interface PlatformTenant { id: string; name: string; publicCode: string; isActive: boolean; establishments: PlatformUnit[] }
export interface PlatformTenantPage { items: PlatformTenant[]; totalCount: number; page: number; pageSize: number }
export interface ProvisioningResult { tenantId: string; establishmentId: string; ownerId: string; tenantPublicCode: string; nextStep: string }
export interface ProvisionTenantInput { intentKey: string; tenantName: string; tenantPublicCode: string; establishmentName: string; establishmentSlug: string; timeZoneId: string; ownerName: string; ownerEmail: string; temporaryPassword: string }
export interface AddUnitInput { intentKey: string; name: string; slug: string; timeZoneId: string; ownerId: string }
export interface PlatformOwner { id: string; name: string; email: string; isActive: boolean; roles: number[] }

export const platformClient = {
  async search(search = '', page = 1, pageSize = 20) {
    return (await api.get<PlatformTenantPage>('/api/platform/tenants', { params: { search: search || undefined, page, pageSize } })).data
  },
  async provision(input: ProvisionTenantInput) {
    return (await api.post<ProvisioningResult>('/api/platform/tenants', input)).data
  },
  async addUnit(tenantId: string, input: AddUnitInput) {
    return (await api.post<ProvisioningResult>(`/api/platform/tenants/${tenantId}/establishments`, input)).data
  },
  async owners(establishmentId: string) {
    const response = await api.get<{ items: PlatformOwner[] }>(`/api/admin/establishments/${establishmentId}/users`, { params: { page: 1, pageSize: 100 } })
    return response.data.items.filter(item => item.isActive && item.roles.includes(1))
  },
  async recover(intentKey: string) {
    return (await api.get<ProvisioningResult>(`/api/platform/tenants/provisioning/${intentKey}`)).data
  }
}
