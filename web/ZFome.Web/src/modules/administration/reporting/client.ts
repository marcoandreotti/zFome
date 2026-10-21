import { api } from '../../../http/client'

export type ServiceType = 'Table' | 'Pickup' | 'Delivery'

export interface DashboardPeriod {
  from: string
  to: string
  revenue: number
  receivedOrders: number
  completedOrders: number
  averageTicket: number
  confirmedPayments: number
  cancelledOrders: number
  rejectedOrders: number
}

export interface DashboardDay {
  date: string
  periodOffset: number
  isComparison: boolean
  revenue: number
  completedOrders: number
  confirmedPayments: number
}

export interface DashboardProduct {
  name: string
  quantitySold: number
  revenue: number
}

export interface ProductPage {
  total: number
  page: number
  pageSize: number
  items: DashboardProduct[]
}

export interface BusinessDashboard {
  timeZoneId: string
  current: DashboardPeriod
  previous: DashboardPeriod
  series: DashboardDay[]
  products: ProductPage
}

export interface DashboardFilters {
  from: string
  to: string
  serviceType: ServiceType | null
  productPage: number
  productPageSize: number
}

const root = (unit: string) =>
  `/api/admin/establishments/${encodeURIComponent(unit)}/reports/dashboard`

export const reportingClient = {
  async dashboard(unit: string, filters: DashboardFilters, signal?: AbortSignal) {
    return (await api.get<BusinessDashboard>(root(unit), { params: filters, signal })).data
  },
  async export(unit: string, filters: Pick<DashboardFilters, 'from' | 'to' | 'serviceType'>) {
    return (await api.get<Blob>(root(unit) + '/export', { params: filters, responseType: 'blob' })).data
  }
}
