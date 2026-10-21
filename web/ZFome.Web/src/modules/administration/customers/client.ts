import { api } from '../../../http/client'
export interface Address {
  id: string
  label: string
  street: string
  number: string
  complement: string | null
  neighborhood: string
  city: string
  state: string
  postalCode: string
  isPrimary: boolean
}
export interface Customer {
  id: string
  name: string
  phone: string
  email: string | null
  addresses: Address[]
}
export interface CustomerPage {
  page: number
  pageSize: number
  total: number
  items: Customer[]
}
const path = (unit: string) =>
  `/api/admin/establishments/${encodeURIComponent(unit)}/customers`
export const customersClient = {
  async search(
    unit: string,
    params: { search: string; page: number; pageSize: number },
    signal?: AbortSignal
  ) {
    return (await api.get<CustomerPage>(path(unit), { params, signal })).data
  },
  async save(
    unit: string,
    id: string | null,
    data: Pick<Customer, 'name' | 'phone' | 'email'>
  ) {
    return (
      await (id
        ? api.put<{ id: string }>(
            `${path(unit)}/${encodeURIComponent(id)}`,
            data
          )
        : api.post<{ id: string }>(path(unit), data))
    ).data
  },
  async address(
    unit: string,
    customerId: string,
    id: string | null,
    data: Omit<Address, 'id'>
  ) {
    const url = `${path(unit)}/${encodeURIComponent(customerId)}/addresses`
    return (
      await (id
        ? api.put<{ id: string }>(`${url}/${encodeURIComponent(id)}`, data)
        : api.post<{ id: string }>(url, data))
    ).data
  },
  async removeAddress(unit: string, customerId: string, id: string) {
    await api.delete(
      `${path(unit)}/${encodeURIComponent(customerId)}/addresses/${encodeURIComponent(id)}`
    )
  }
}
