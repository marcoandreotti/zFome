export type KitchenTicketStatus = 'Confirmed' | 'Preparing'
export type KitchenTicketAction = 'StartPreparation' | 'MarkReady'
export type KitchenServiceType = 'Table' | 'Pickup' | 'Delivery'

export interface KitchenAdditional {
  name: string
  quantity: number
}

export interface KitchenItem {
  id: string
  productName: string
  variationName: string | null
  quantity: number
  notes: string | null
  additionals: KitchenAdditional[]
}

export interface KitchenTicket {
  id: string
  number: number
  serviceType: KitchenServiceType
  status: KitchenTicketStatus
  customerName: string | null
  tableCode: string | null
  confirmedAt: string
  preparationStartedAt: string | null
  action: KitchenTicketAction
  items: KitchenItem[]
}
