import { flushPromises, mount } from '@vue/test-utils'
import { expect, it, vi } from 'vitest'
import CatalogPicker from '../src/modules/administration/catalog/CatalogPicker.vue'
import { catalogClient } from '../src/modules/administration/catalog/client'
import { ApiError } from '../src/http/client'
import { adminStubs } from './admin-stubs'

vi.mock('../src/modules/administration/catalog/client', () => ({
  catalogClient: { search: vi.fn() }
}))

it('retira resultados antigos após falha e permite recuperar a pesquisa', async () => {
  vi.mocked(catalogClient.search)
    .mockResolvedValueOnce({
      items: [
        { id: 'old', name: 'Anterior', price: 1, order: 0, isActive: true }
      ],
      total: 1,
      page: 1,
      pageSize: 20
    })
    .mockRejectedValueOnce(new ApiError({ status: 403 }))
    .mockResolvedValueOnce({ items: [], total: 0, page: 1, pageSize: 20 })
  const wrapper = mount(CatalogPicker, {
    props: { unitId: 'unit', resource: 'additionals', excludedIds: [] },
    global: {
      stubs: {
        ...adminStubs,
        QList: { template: '<div><slot /></div>' },
        QItem: { template: '<div><slot /></div>' },
        QItemSection: { template: '<div><slot /></div>' }
      }
    }
  })
  await flushPromises()
  expect(wrapper.text()).toContain('Anterior')
  await wrapper.get('form').trigger('submit')
  await flushPromises()
  expect(wrapper.text()).not.toContain('Anterior')
  expect(wrapper.text()).not.toContain('Nenhum resultado')
  expect(wrapper.get('[role="alert"]').text()).toContain('permissão')
  await wrapper
    .findAll('button')
    .find((button) => button.text() === 'Tentar novamente')!
    .trigger('click')
  await flushPromises()
  expect(wrapper.find('[role="alert"]').exists()).toBe(false)
  expect(wrapper.text()).toContain('Nenhum resultado')
  wrapper.unmount()
})

it.each([
  [400, 'Revise os campos'],
  [401, 'sessão expirou'],
  [403, 'permissão'],
  [404, 'não foi encontrado'],
  [409, 'conflito'],
  [503, 'Tente novamente']
])(
  'explica HTTP %s quando o servidor não fornece descrição',
  (status, message) => {
    expect(new ApiError({ status: Number(status) }).message).toContain(message)
  }
)
it('preserva a explicação autoritativa do servidor', () => {
  expect(
    new ApiError({ status: 409, detail: 'Último Owner ativo.' }).message
  ).toBe('Último Owner ativo.')
})
