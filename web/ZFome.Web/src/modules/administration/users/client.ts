import { api } from '../../../http/client'

export interface AdministrativeUser {
  id: string
  name: string
  email: string
  isActive: boolean
  roles: number[]
  establishmentIds: string[]
  isCurrentUser: boolean
}
export interface UserPage {
  items: AdministrativeUser[]
  totalCount: number
  page: number
  pageSize: number
}
export interface UserSearch {
  search: string
  isActive?: boolean
  associatedOnly: boolean
  page: number
  pageSize: number
}
export const roles = [
  { value: 1, label: 'Owner' },
  { value: 2, label: 'Admin' },
  { value: 3, label: 'Gerente' },
  { value: 4, label: 'Atendente' },
  { value: 5, label: 'Cozinha' },
  { value: 6, label: 'Entregador' }
]
export function canManageOwner(
  user: AdministrativeUser,
  ownership: boolean,
  platform: boolean
) {
  return platform || (ownership && !user.isCurrentUser)
}
const path = (unitId: string) =>
  `/api/admin/establishments/${encodeURIComponent(unitId)}/users`
export const usersClient = {
  async search(unitId: string, params: UserSearch, signal?: AbortSignal) {
    return (await api.get<UserPage>(path(unitId), { params, signal })).data
  },
  async create(
    unitId: string,
    request: {
      name: string
      email: string
      password: string
      initialRole: number
    }
  ) {
    await api.post(path(unitId), request)
  },
  async update(unitId: string, id: string, name: string) {
    await api.put(`${path(unitId)}/${id}`, { name })
  },
  async active(unitId: string, id: string, isActive: boolean) {
    await api.patch(`${path(unitId)}/${id}/active`, { isActive })
  },
  async role(unitId: string, id: string, role: number, granted: boolean) {
    await api.put(`${path(unitId)}/${id}/roles/${role}`, { granted })
  },
  async access(unitId: string, id: string, granted: boolean) {
    await api.put(`${path(unitId)}/${id}/access`, { granted })
  }
}
