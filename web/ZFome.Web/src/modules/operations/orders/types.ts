export type OrderStatus =
  | 'Confirmed'
  | 'Preparing'
  | 'Ready'
  | 'OutForDelivery'
  | 'Completed'
  | 'Cancelled'
  | 'Rejected'

export type OrderServiceType = 'Table' | 'Pickup' | 'Delivery'

export interface OrderSummary {
  id: string
  number: number
  serviceType: OrderServiceType
  status: OrderStatus
  customerName: string | null
  customerPhone: string | null
  total: number
  createdAt: string
  scheduledAtUtc?: string | null
  scheduledTimeZoneId?: string | null
  items?: OrderSummaryItem[]
}

export interface OrderSummaryItem {
  quantity: number
  productName: string
  variationName: string | null
}

export interface OrderAdditional {
  name: string
  unitPrice: number
  quantity: number
}

export interface OrderItem {
  id: string
  productName: string
  variationName: string | null
  unitPrice: number
  quantity: number
  total: number
  notes: string | null
  additionals: OrderAdditional[]
}

export interface OrderHistory {
  previousStatus: OrderStatus
  newStatus: OrderStatus
  occurredAt: string
  actorId: string | null
  note: string | null
}

export interface OrderDetail {
  id: string
  number: number | null
  publicReference: string | null
  serviceType: OrderServiceType
  status: OrderStatus
  customerName: string | null
  customerPhone: string | null
  tableCode: string | null
  subtotal: number
  discount: number
  fees: number
  total: number
  couponCode: string | null
  confirmedAmount: number
  isFullyPaid: boolean
  items: OrderItem[]
  history: OrderHistory[]
  deliveryAddress?: { street: string; number: string; complement: string | null; neighborhood: string; city: string; state: string; postalCode: string } | null
  deliveryRegionId?: string | null
  deliveryRegionName?: string | null
  deliveryFee?: number
  deliveryEstimatedMinutes?: number | null
  scheduledAtUtc?: string | null
  scheduledTimeZoneId?: string | null
}

export interface OrderPage {
  page: number
  pageSize: number
  total: number
  items: OrderSummary[]
}

export interface OrderFilters {
  from?: string
  to?: string
  status?: OrderStatus
  serviceType?: OrderServiceType
  number?: number
}
