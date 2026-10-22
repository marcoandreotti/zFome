import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useSessionStore } from '../src/modules/session/store'
import CatalogPage from '../src/modules/administration/catalog/CatalogPage.vue'
import { catalogClient } from '../src/modules/administration/catalog/client'
import { ApiError } from '../src/http/client'
import { adminStubs as stubs } from './admin-stubs'
vi.mock('../src/modules/administration/catalog/client', async (original) => ({
  ...(await original<
    typeof import('../src/modules/administration/catalog/client')
  >()),
  catalogClient: { get: vi.fn(), search: vi.fn(), save: vi.fn() }
}))
beforeEach(() => {
  vi.clearAllMocks()
  setActivePinia(createPinia())
  useSessionStore().unitId = 'unit'
  vi.mocked(catalogClient.get).mockResolvedValue({
    establishmentId: 'unit',
    establishmentName: 'Unit',
    slug: 'unit',
    categories: []
  })
  vi.mocked(catalogClient.search).mockResolvedValue({
    total: 0,
    page: 1,
    pageSize: 20,
    items: []
  })
})
it('preserva produto e código no conflito; cancelar confirmação não grava', async () => {
  const wrapper = mount(CatalogPage, { global: { stubs } })
  await flushPromises()
  wrapper
    .findComponent({ name: 'QTabs' })
    .vm.$emit('update:modelValue', 'products')
  await flushPromises()
  await wrapper
    .findAll('button')
    .find((x) => x.text() === 'Cadastrar')!
    .trigger('click')
  const code = wrapper
    .findAll('label')
    .find((x) => x.text() === 'Código')!
    .find('input')
  await code.setValue('P-REPETIDO')
  await wrapper.findAll('form')[1]!.trigger('submit')
  await wrapper
    .findAll('button')
    .find((x) => x.text() === 'Voltar')!
    .trigger('click')
  expect(catalogClient.save).not.toHaveBeenCalled()
  vi.mocked(catalogClient.save).mockRejectedValue(
    new ApiError({ status: 409, detail: 'Código já cadastrado.' })
  )
  await wrapper.findAll('form')[1]!.trigger('submit')
  await wrapper
    .findAll('button')
    .find((x) => x.text() === 'Confirmar')!
    .trigger('click')
  await flushPromises()
  expect(wrapper.text()).toContain('Código já cadastrado.')
  expect((code.element as HTMLInputElement).value).toBe('P-REPETIDO')
  expect(catalogClient.save).toHaveBeenCalledTimes(1)
  wrapper.unmount()
})
it('recarrega adicional cadastrado sem vínculo pela consulta independente', async () => {
  const wrapper = mount(CatalogPage, { global: { stubs } })
  await flushPromises()
  wrapper
    .findComponent({ name: 'QTabs' })
    .vm.$emit('update:modelValue', 'additionals')
  await flushPromises()
  await wrapper
    .findAll('button')
    .find((x) => x.text() === 'Cadastrar')!
    .trigger('click')
  await wrapper
    .findAll('label')
    .find((x) => x.text() === 'Nome')!
    .find('input')
    .setValue('Novo adicional')
  vi.mocked(catalogClient.save).mockResolvedValue({ id: 'new' })
  vi.mocked(catalogClient.search).mockResolvedValue({
    total: 1,
    page: 1,
    pageSize: 20,
    items: [
      { id: 'new', name: 'Novo adicional', price: 0, order: 0, isActive: true }
    ]
  })
  await wrapper.findAll('form')[1]!.trigger('submit')
  await wrapper
    .findAll('button')
    .find((x) => x.text() === 'Confirmar')!
    .trigger('click')
  await flushPromises()
  expect(catalogClient.search).toHaveBeenLastCalledWith(
    'unit',
    'additionals',
    expect.objectContaining({ page: 1 }),
    expect.any(AbortSignal)
  )
  expect(wrapper.find('tbody').text()).toContain('Novo adicional')
  wrapper.unmount()
})

it('alterna as visões de produtos sem refazer a consulta nem perder os filtros', async () => {
  vi.mocked(catalogClient.get).mockResolvedValue({
    establishmentId: 'unit',
    establishmentName: 'Unit',
    slug: 'unit',
    categories: [
      {
        id: 'category',
        parentId: null,
        name: 'Pizzas',
        description: null,
        order: 0,
        imageUrl: null,
        isActive: true,
        products: [
          {
            id: 'product',
            code: 'P-1',
            name: 'Pizza da casa',
            description: null,
            basePrice: 39.9,
            isFeatured: false,
            isActive: false,
            allowsNotes: true,
            images: [],
            variations: Array.from({ length: 7 }, (_, index) => ({
              id: `variation-${index}`,
              name: `Tamanho ${index}`,
              price: 39.9 + index,
              order: index,
              isActive: true
            })),
            additionalGroups: Array.from({ length: 5 }, (_, index) => ({
              id: `group-${index}`,
              name: `Grupo ${index}`,
              minimumSelection: 0,
              maximumSelection: 1,
              isActive: true,
              order: index,
              items: []
            }))
          }
        ]
      }
    ]
  })
  const wrapper = mount(CatalogPage, { global: { stubs } })
  await flushPromises()
  wrapper.findComponent({ name: 'QTabs' }).vm.$emit('update:modelValue', 'products')
  await flushPromises()
  const search = wrapper.findAll('label').find((item) => item.text() === 'Pesquisar nome')!.find('input')
  await search.setValue('Pizza')
  await wrapper.find('form').trigger('submit')
  await flushPromises()
  const callsAfterFilter = vi.mocked(catalogClient.get).mock.calls.length

  await wrapper.findAll('button').find((item) => item.text() === 'Visual')!.trigger('click')
  expect(wrapper.text()).toContain('Pizza da casa')
  expect(wrapper.text()).toContain('Pizzas')
  expect(wrapper.text()).toContain('R$ 39,90')
  expect(wrapper.text()).toContain('Inativo')
  expect(wrapper.text()).toContain('7 variações · 5 grupos')
  expect(wrapper.find('.admin-product-card__placeholder').exists()).toBe(true)

  await wrapper.findAll('button').find((item) => item.text() === 'Compacta')!.trigger('click')
  expect((search.element as HTMLInputElement).value).toBe('Pizza')
  expect(vi.mocked(catalogClient.get)).toHaveBeenCalledTimes(callsAfterFilter)
  expect(wrapper.find('table').text()).toContain('Pizza da casa')
  wrapper.unmount()
})
