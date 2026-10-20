import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useSessionStore } from '../src/modules/session/store'
import CustomersPage from '../src/modules/administration/customers/CustomersPage.vue'
import {
  customersClient,
  type Address
} from '../src/modules/administration/customers/client'
import { ApiError } from '../src/http/client'
import { adminStubs as stubs } from './admin-stubs'
vi.mock('../src/modules/administration/customers/client', () => ({
  customersClient: {
    search: vi.fn(),
    save: vi.fn(),
    address: vi.fn(),
    removeAddress: vi.fn()
  }
}))
const address: Address = {
  id: 'home',
  label: 'Casa',
  street: 'Rua A',
  number: '1',
  complement: null,
  neighborhood: 'Centro',
  city: 'Cidade',
  state: 'SP',
  postalCode: '00000-000',
  isPrimary: true
}
const customer = {
  id: 'customer',
  name: 'Maria',
  phone: '11999999999',
  email: null,
  addresses: [
    address,
    { ...address, id: 'work', label: 'Trabalho', isPrimary: false }
  ]
}
beforeEach(() => {
  vi.clearAllMocks()
  setActivePinia(createPinia())
  useSessionStore().unitId = 'unit'
  vi.mocked(customersClient.search).mockResolvedValue({
    total: 21,
    page: 1,
    pageSize: 20,
    items: [customer]
  })
})
it('pesquisa na página solicitada e mantém o contato após erro', async () => {
  const wrapper = mount(CustomersPage, { global: { stubs } })
  await flushPromises()
  await wrapper
    .findAll('button')
    .find((x) => x.text() === 'Página 2')!
    .trigger('click')
  await flushPromises()
  expect(customersClient.search).toHaveBeenLastCalledWith(
    'unit',
    expect.objectContaining({ page: 2 }),
    expect.any(AbortSignal)
  )
  await wrapper
    .findAll('button')
    .find((x) => x.text() === 'Editar')!
    .trigger('click')
  const name = wrapper
    .findAll('label')
    .find((x) => x.text() === 'Nome')!
    .find('input')
  await name.setValue('Maria editada')
  const phone = wrapper
    .findAll('label')
    .find((x) => x.text() === 'Telefone')!
    .find('input')
  await phone.setValue('+55 (0) 19 99999-9999')
  vi.mocked(customersClient.save).mockRejectedValue(
    new ApiError({
      status: 400,
      detail: 'Telefone inválido.',
      errors: { Phone: ['Confira o telefone.'] }
    })
  )
  await wrapper.findAll('form')[1]!.trigger('submit')
  await wrapper
    .findAll('button')
    .find((x) => x.text() === 'Confirmar')!
    .trigger('click')
  await flushPromises()
  expect(customersClient.save).toHaveBeenCalledWith(
    'unit',
    'customer',
    expect.objectContaining({ phone: '19999999999' })
  )
  expect(wrapper.text()).toContain('Telefone inválido.')
  expect((name.element as HTMLInputElement).value).toBe('Maria editada')
  wrapper.unmount()
})
it('envia principal explicitamente e recarrega o resultado autoritativo', async () => {
  const wrapper = mount(CustomersPage, { global: { stubs } })
  await flushPromises()
  await wrapper
    .findAll('button')
    .find((x) => x.text() === 'Endereços')!
    .trigger('click')
  await wrapper
    .findAll('button')
    .filter((x) => x.text() === 'Editar endereço')[1]!
    .trigger('click')
  await wrapper.find('input[type=checkbox]').setValue(true)
  vi.mocked(customersClient.address).mockResolvedValue({ id: 'work' })
  vi.mocked(customersClient.search).mockResolvedValue({
    total: 1,
    page: 1,
    pageSize: 20,
    items: [
      {
        ...customer,
        addresses: [
          { ...address, isPrimary: false },
          { ...address, id: 'work', label: 'Trabalho', isPrimary: true }
        ]
      }
    ]
  })
  await wrapper.findAll('form')[1]!.trigger('submit')
  await wrapper
    .findAll('button')
    .find((x) => x.text() === 'Confirmar')!
    .trigger('click')
  await flushPromises()
  expect(customersClient.address).toHaveBeenCalledWith(
    'unit',
    'customer',
    'work',
    expect.objectContaining({ isPrimary: true })
  )
  await wrapper
    .findAll('button')
    .find((x) => x.text() === 'Endereços')!
    .trigger('click')
  expect(wrapper.text()).toContain('Trabalho — Principal')
  expect(wrapper.text()).not.toContain('Casa — Principal')
  await wrapper
    .findAll('button')
    .find((x) => x.text() === 'Remover endereço')!
    .trigger('click')
  await wrapper
    .findAll('button')
    .find((x) => x.text() === 'Voltar')!
    .trigger('click')
  expect(customersClient.removeAddress).not.toHaveBeenCalled()
  wrapper.unmount()
})
