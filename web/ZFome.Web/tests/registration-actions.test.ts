import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick, type Component } from 'vue'
import { useSessionStore } from '../src/modules/session/store'
import CatalogPage from '../src/modules/administration/catalog/CatalogPage.vue'
import CustomersPage from '../src/modules/administration/customers/CustomersPage.vue'
import CouponsPage from '../src/modules/administration/coupons/CouponsPage.vue'
import PaymentMethodsPage from '../src/modules/administration/payment-methods/PaymentMethodsPage.vue'
import UsersPage from '../src/modules/administration/users/UsersPage.vue'
import { catalogClient } from '../src/modules/administration/catalog/client'
import { customersClient } from '../src/modules/administration/customers/client'
import { couponsClient } from '../src/modules/administration/coupons/client'
import { paymentMethodsClient } from '../src/modules/administration/payment-methods/client'
import { usersClient } from '../src/modules/administration/users/client'
import { adminStubs as stubs } from './admin-stubs'

vi.mock('../src/modules/administration/catalog/client', async (original) => ({
  ...(await original<
    typeof import('../src/modules/administration/catalog/client')
  >()),
  catalogClient: { get: vi.fn(), search: vi.fn(), save: vi.fn() }
}))
vi.mock('../src/modules/administration/customers/client', () => ({
  customersClient: {
    search: vi.fn(),
    save: vi.fn(),
    address: vi.fn(),
    removeAddress: vi.fn()
  }
}))
vi.mock('../src/modules/administration/coupons/client', async (original) => ({
  ...(await original<
    typeof import('../src/modules/administration/coupons/client')
  >()),
  couponsClient: { search: vi.fn(), save: vi.fn(), active: vi.fn() }
}))
vi.mock('../src/modules/administration/payment-methods/client', () => ({
  paymentMethodsClient: { search: vi.fn(), save: vi.fn(), active: vi.fn() }
}))
vi.mock('../src/modules/administration/users/client', async (original) => ({
  ...(await original<
    typeof import('../src/modules/administration/users/client')
  >()),
  usersClient: {
    search: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    active: vi.fn(),
    role: vi.fn(),
    access: vi.fn()
  }
}))

const registrations: Array<{
  name: string
  component: Component
  action: string
}> = [
  {
    name: 'catálogo',
    component: CatalogPage,
    action: 'Cadastrar'
  },
  {
    name: 'clientes',
    component: CustomersPage,
    action: 'Cadastrar cliente'
  },
  {
    name: 'cupons',
    component: CouponsPage,
    action: 'Cadastrar cupom'
  },
  {
    name: 'formas de pagamento',
    component: PaymentMethodsPage,
    action: 'Cadastrar forma'
  },
  {
    name: 'usuários',
    component: UsersPage,
    action: 'Novo usuário'
  }
]

const pending = () => new Promise<never>(() => undefined)

beforeEach(() => {
  vi.clearAllMocks()
  setActivePinia(createPinia())
  vi.mocked(catalogClient.get).mockReturnValue(pending())
  vi.mocked(customersClient.search).mockReturnValue(pending())
  vi.mocked(couponsClient.search).mockReturnValue(pending())
  vi.mocked(paymentMethodsClient.search).mockReturnValue(pending())
  vi.mocked(usersClient.search).mockReturnValue(pending())
})

describe.each(registrations)('$name', ({ component, action }) => {
  it('permite iniciar cadastro enquanto a listagem carrega', async () => {
    useSessionStore().unitId = 'unit'
    const wrapper = mount(component, { global: { stubs } })
    await nextTick()

    const button = wrapper
      .findAll('button')
      .find((candidate) => candidate.text() === action)!
    expect(button.attributes('disabled')).toBeUndefined()

    await button.trigger('click')
    expect(wrapper.findAll('form').length).toBeGreaterThan(1)
    wrapper.unmount()
  })

  it('mantém cadastro indisponível sem unidade autorizada', async () => {
    const wrapper = mount(component, { global: { stubs } })
    await nextTick()

    const button = wrapper
      .findAll('button')
      .find((candidate) => candidate.text() === action)!
    expect(button.attributes('disabled')).toBeDefined()

    await button.trigger('click')
    expect(wrapper.findAll('form')).toHaveLength(1)
    wrapper.unmount()
  })
})
