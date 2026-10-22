import { flushPromises, mount } from '@vue/test-utils'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import {
  catalogClient,
  groupPayload,
  productPayload,
  type Group,
  type Product
} from '../src/modules/administration/catalog/client'
import CatalogPicker from '../src/modules/administration/catalog/CatalogPicker.vue'
vi.mock(
  '../src/modules/administration/catalog/client',
  async (importOriginal) => ({
    ...(await importOriginal<
      typeof import('../src/modules/administration/catalog/client')
    >()),
    catalogClient: { search: vi.fn() }
  })
)
const group: Group = {
  id: 'group',
  name: 'Grupo',
  isActive: false,
  minimumSelection: 0,
  maximumSelection: 2,
  order: 4,
  items: [
    { id: 'inactive', name: 'Inativo', price: 3, isActive: false, order: 7 }
  ]
}
describe('edição de catálogo', () => {
  it('preserva IDs e ordem de itens inativos do grupo', () => {
    expect(groupPayload(group).items).toEqual([
      { additionalId: 'inactive', order: 7, compatibilityRules: [] }
    ])
  })
  it('preserva grupos e variações inativos no produto', () => {
    const product: Product = {
      id: 'p',
      name: 'Produto',
      code: 'P1',
      description: null,
      basePrice: 5,
      isActive: true,
      isFeatured: false,
      allowsNotes: true,
      images: [],
      variations: [{ name: 'Variação', price: 10, isActive: false, order: 1 }],
      additionalGroups: [group]
    }
    expect(productPayload(product, 'category').additionalGroups).toEqual([
      { groupId: 'group', order: 4 }
    ])
    expect(productPayload(product, 'category').variations[0]?.isActive).toBe(
      false
    )
  })
})
describe('seleção paginada do catálogo', () => {
  beforeEach(() => vi.clearAllMocks())
  it('consulta segunda página sem filtrar recursos inativos e permite selecionar', async () => {
    vi.mocked(catalogClient.search)
      .mockResolvedValueOnce({ items: [], page: 1, pageSize: 20, total: 21 })
      .mockResolvedValueOnce({
        items: [group],
        page: 2,
        pageSize: 20,
        total: 21
      })
    const wrapper = mount(CatalogPicker, {
      props: { unitId: 'unit', resource: 'additional-groups', excludedIds: [] },
      global: {
        stubs: {
          QCard: { template: '<div><slot /></div>' },
          QCardSection: { template: '<div><slot /></div>' },
          QCardActions: { template: '<div><slot /></div>' },
          QForm: { template: '<form><slot /></form>' },
          QInput: true,
          QBanner: true,
          QBtn: {
            props: ['label'],
            emits: ['click'],
            template: '<button @click="$emit(\'click\')">{{ label }}</button>'
          },
          QList: { template: '<div><slot /></div>' },
          QItem: { template: '<div><slot /></div>' },
          QItemSection: { template: '<div><slot /></div>' },
          QPagination: {
            template:
              '<button data-next @click="$emit(\'update:modelValue\', 2)">Próxima</button>'
          }
        }
      }
    })
    await flushPromises()
    await wrapper.get('[data-next]').trigger('click')
    await flushPromises()
    expect(catalogClient.search).toHaveBeenLastCalledWith(
      'unit',
      'additional-groups',
      { search: '', page: 2, pageSize: 20 },
      expect.any(AbortSignal)
    )
    expect(wrapper.text()).toContain('Inativo')
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Selecionar')!
      .trigger('click')
    expect(wrapper.emitted('selected')).toEqual([[group]])
    wrapper.unmount()
  })
})
