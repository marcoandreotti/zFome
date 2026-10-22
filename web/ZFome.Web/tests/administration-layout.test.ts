import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, expect, it, vi } from 'vitest'
import AdministrationLayout from '../src/layouts/AdministrationLayout.vue'
import { useSessionStore } from '../src/modules/session/store'

const replace = vi.fn()
vi.mock('vue-router', () => ({
  useRoute: () => ({ path: '/administration/catalog' }),
  useRouter: () => ({ push: vi.fn(), replace })
}))

const passthrough = { template: '<div><slot /></div>' }
const stubs = {
  QLayout: passthrough,
  QHeader: passthrough,
  QToolbar: passthrough,
  QToolbarTitle: passthrough,
  QDrawer: passthrough,
  QList: passthrough,
  QItemSection: passthrough,
  QPageContainer: passthrough,
  RouterView: passthrough,
  QBtn: {
    props: ['label'],
    emits: ['click'],
    template: '<button @click="$emit(\'click\')">{{ label }}<slot /></button>'
  },
  QItem: {
    template: '<a><slot /></a>'
  },
  QSelect: {
    name: 'QSelect',
    props: ['modelValue', 'options'],
    emits: ['update:modelValue'],
    template: '<select :value="modelValue" @change="$emit(\'update:modelValue\', $event.target.value)"><option v-for="item in options" :value="item.id">{{ item.name }}</option></select>'
  }
}

beforeEach(() => {
  setActivePinia(createPinia())
  sessionStorage.clear()
  vi.clearAllMocks()
})

it('mantém unidade autorizada visível, filtra a navegação e restaura foco ao trocar', async () => {
  const session = useSessionStore()
  session.context = {
    passwordChangeRequired: false,
    isPlatformUser: false,
    capabilities: ['management'],
    establishments: [
      { id: 'one', name: 'Centro' },
      { id: 'two', name: 'Norte' }
    ]
  }
  session.selectUnit('one')
  const focus = vi.spyOn(HTMLElement.prototype, 'focus')
  const wrapper = mount(AdministrationLayout, {
    attachTo: document.body,
    global: { stubs }
  })

  expect(wrapper.text()).toContain('zFome')
  expect(wrapper.text()).toContain('Centro')
  expect(wrapper.text()).toContain('Catálogo')
  expect(wrapper.text()).not.toContain('Usuários')
  expect(wrapper.text()).not.toContain('Cupons')

  wrapper.findComponent({ name: 'QSelect' }).vm.$emit('update:modelValue', 'two')
  await flushPromises()
  expect(wrapper.text()).toContain('Norte')
  expect(focus).toHaveBeenCalled()
  focus.mockRestore()
  wrapper.unmount()
})
