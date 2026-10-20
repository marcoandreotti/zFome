export interface PublicTheme {
  primaryColor: string
  secondaryColor: string
  backgroundColor: string
  textColor: string
  fontFamily: string
  logoUrl: string | null
}
export interface PublicPaymentMethod { id: string; code: string; name: string; isOnline: boolean; allowsChange: boolean }
export interface PublicContext {
  establishmentName: string; slug: string; theme: PublicTheme
  table: { code: string; token: string } | null
  paymentMethods: PublicPaymentMethod[]
  availability?: PublicServiceAvailability[]
}
export interface PublicServiceAvailability {
  serviceType: ServiceType
  isAvailable: boolean
  reason: string
  message: string | null
  nextOpening: string | null
}
export interface Additional {
  id: string; name: string; price: number; isActive: boolean; order: number
  isAvailable?: boolean; unavailabilityReason?: string | null; availableAgainAt?: string | null
  compatibilityRules?: { targetGroupId: string; targetAdditionalId: string; kind: 'Requires' | 'Excludes' }[]
}
export interface AdditionalGroup {
  id: string; name: string; minimumSelection: number; maximumSelection: number
  isActive: boolean; order: number; items: Additional[]
  pricingStrategy?: 'Additive' | 'HighestPrice' | 'Proportional' | 'NoPriceChange'
  type?: 'Additional' | 'Flavor' | 'Crust' | 'Removal'
  requiresCompleteComposition?: boolean
}
export interface ProductVariation {
  id: string; name: string; price: number; order: number; isActive: boolean
  isAvailable?: boolean; unavailabilityReason?: string | null; availableAgainAt?: string | null
}
export interface Product {
  id: string; code: string; name: string; description: string | null
  basePrice: number; isFeatured: boolean; isActive: boolean; allowsNotes: boolean
  ratingAverage?: number | null; ratingCount?: number
  images: { id: string; url: string; order: number; isPrincipal: boolean }[]
  variations: ProductVariation[]; additionalGroups: AdditionalGroup[]
  isAvailable?: boolean; unavailabilityReason?: string | null; availableAgainAt?: string | null
}
export interface PublicCatalog {
  establishmentId: string; establishmentName: string; slug: string
  categories: PublicCategory[]
}
export interface PublicCategory {
  id: string; parentId: string | null; name: string; description: string | null
  order: number; imageUrl: string | null; isActive: boolean; products: Product[]
}
export interface Address {
  label: string; street: string; number: string; complement: string | null
  neighborhood: string; city: string; state: string; postalCode: string
}
export interface OrderItemRequest {
  productId: string; variationId: string | null; quantity: number; notes: string | null
  additionals: { additionalId: string; quantity: number; groupId: string | null; portionNumerator: number | null; portionDenominator: number | null }[]
}
export type ServiceType = 'Table' | 'Pickup' | 'Delivery'
export interface SimulationRequest {
  serviceType: ServiceType; customerId: string | null; customerAddressId: string | null
  tableToken: string | null; deliveryAddress: Address | null; couponCode: string | null
  paymentMethodId: string | null; items: OrderItemRequest[]; scheduledAtUtc?: string | null
}
export interface SchedulingSlots {
  serviceType: ServiceType; isEnabled: boolean; timeZoneId: string; slotIntervalMinutes: number
  minimumAdvanceMinutes: number; horizonDays: number
  slots: { startsAt: string; remainingCapacity: number | null }[]
}
export interface SimulatedItem {
  productName: string; variationName: string | null; unitPrice: number; quantity: number
  total: number; additionals: { name: string; unitPrice: number; quantity: number }[]
  basePrice?: number
  modifierGroups?: { groupId: string; name: string; pricingStrategy: string; price: number; options: { optionId: string; name: string; unitPrice: number; quantity: number; portionNumerator: number | null; portionDenominator: number | null }[] }[] | null
}
export interface Simulation {
  subtotal: number; discount: number; fees: number; total: number
  couponCode: string | null; items: SimulatedItem[]
  deliveryRegionId?: string | null; deliveryRegionName?: string | null; deliveryFee?: number | null
  deliveryEstimatedMinutes?: number | null; deliveryQuoteIssuedAt?: string | null
  scheduledAtUtc?: string | null; scheduledTimeZoneId?: string | null
}
export interface ConfirmationRequest extends Omit<SimulationRequest, 'paymentMethodId'> {
  paymentMethodId: string; receivedAmount: number | null
  deliveryRegionId?: string | null; expectedDeliveryFee?: number | null
  expectedDeliveryEstimatedMinutes?: number | null; deliveryQuoteIssuedAt?: string | null
}
export interface Confirmation { reference: string; number: number; status: string; total: number }
export interface Tracking extends Confirmation {
  serviceType: ServiceType; subtotal: number; discount: number; fees: number
  couponCode: string | null; items: SimulatedItem[]
  deliveryRegionName?: string | null; deliveryFee?: number
  deliveryEstimatedMinutes?: number | null
  scheduledAtUtc?: string | null; scheduledTimeZoneId?: string | null
  history: { status: string; occurredAt: string; note: string | null }[]
}
