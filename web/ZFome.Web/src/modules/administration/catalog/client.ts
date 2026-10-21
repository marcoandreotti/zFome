import { api } from '../../../http/client'
export interface Page<T> {
  page: number
  pageSize: number
  total: number
  items: T[]
}
export interface Additional {
  id: string
  name: string
  price: number
  isActive: boolean
  order: number
  isAvailable?: boolean
  unavailabilityReason?: string | null
  availableAgainAt?: string | null
  compatibilityRules?: { targetGroupId: string; targetAdditionalId: string; kind: 'Requires' | 'Excludes' }[]
}
export interface Group {
  id: string
  name: string
  minimumSelection: number
  maximumSelection: number
  isActive: boolean
  order: number
  items: Additional[]
  pricingStrategy?: 'Additive' | 'HighestPrice' | 'Proportional' | 'NoPriceChange'
  type?: 'Additional' | 'Flavor' | 'Crust' | 'Removal'
  requiresCompleteComposition?: boolean
  compatibilityRules?: { targetGroupId: string; targetAdditionalId: string; kind: 'Requires' | 'Excludes' }[]
}
export interface Product {
  id: string
  code: string
  name: string
  description: string | null
  basePrice: number
  isFeatured: boolean
  isActive: boolean
  allowsNotes: boolean
  images: { url: string; order: number; isPrincipal: boolean }[]
  variations: {
    id?: string
    name: string
    price: number
    order: number
    isActive: boolean
    isAvailable?: boolean
    unavailabilityReason?: string | null
    availableAgainAt?: string | null
  }[]
  additionalGroups: Group[]
  isAvailable?: boolean
  unavailabilityReason?: string | null
  availableAgainAt?: string | null
}
export interface Category {
  id: string
  parentId: string | null
  name: string
  description: string | null
  order: number
  imageUrl: string | null
  isActive: boolean
  products: Product[]
}
export interface Catalog {
  establishmentId: string
  establishmentName: string
  slug: string
  categories: Category[]
}
export type Resource =
  'categories' | 'products' | 'additionals' | 'additional-groups'
export type ReusableResource = 'additionals' | 'additional-groups'
export type ReusableItem = Additional | Group
export interface Search {
  search: string
  isActive?: boolean
  page: number
  pageSize: number
}
const path = (unit: string) =>
  `/api/admin/establishments/${encodeURIComponent(unit)}/catalog`
export const catalogClient = {
  async get(unit: string, signal?: AbortSignal) {
    return (await api.get<Catalog>(path(unit) + '/', { signal })).data
  },
  async search(
    unit: string,
    resource: ReusableResource,
    params: Search,
    signal?: AbortSignal
  ) {
    return (
      await api.get<Page<ReusableItem>>(`${path(unit)}/${resource}`, {
        params,
        signal
      })
    ).data
  },
  async save(
    unit: string,
    resource: Resource,
    id: string | null,
    payload: object
  ) {
    const url = `${path(unit)}/${resource}${id ? '/' + encodeURIComponent(id) : ''}`
    return (
      await (id
        ? api.put<{ id: string }>(url, payload)
        : api.post<{ id: string }>(url, payload))
    ).data
  }
}
// IDs e itens inativos são mantidos; a edição de um vínculo não reconstrói o grupo a partir da página de busca.
export const groupPayload = (group: Group) => ({
  name: group.name,
  minimumSelection: group.minimumSelection,
  maximumSelection: group.maximumSelection,
  isActive: group.isActive,
  pricingStrategy: group.pricingStrategy ?? 'Additive',
  type: group.type ?? 'Additional',
  requiresCompleteComposition: (group.pricingStrategy === 'HighestPrice' || group.pricingStrategy === 'Proportional') && (group.requiresCompleteComposition ?? false),
  items: group.items.map((item) => ({
    additionalId: item.id,
    order: item.order,
    compatibilityRules: item.compatibilityRules ?? []
  }))
})
export const productPayload = (product: Product, categoryId: string) => ({
  categoryId,
  code: product.code,
  name: product.name,
  description: product.description || null,
  basePrice: product.basePrice,
  isFeatured: product.isFeatured,
  isActive: product.isActive,
  allowsNotes: product.allowsNotes,
  images: product.images.map(({ url, order, isPrincipal }) => ({
    url,
    order,
    isPrincipal
  })),
  variations: product.variations.map(({ name, price, order, isActive }) => ({
    name,
    price,
    order,
    isActive
  })),
  additionalGroups: product.additionalGroups.map((group) => ({
    groupId: group.id,
    order: group.order
  }))
})
