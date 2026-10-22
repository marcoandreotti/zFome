import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useSessionStore } from '../src/modules/session/store'
import {
  canManageOwner,
  usersClient,
  type AdministrativeUser
} from '../src/modules/administration/users/client'
import UserPermissions from '../src/modules/administration/users/UserPermissions.vue'
import UsersPage from '../src/modules/administration/users/UsersPage.vue'
import { ApiError } from '../src/http/client'

vi.mock(
  '../src/modules/administration/users/client',
  async (importOriginal) => {
    const original =
      await importOriginal<
        typeof import('../src/modules/administration/users/client')
      >()
    return {
      ...original,
      usersClient: {
        search: vi.fn(),
        create: vi.fn(),
        update: vi.fn(),
        active: vi.fn(),
        role: vi.fn(),
        access: vi.fn()
      }
    }
  }
)
const owner: AdministrativeUser = {
  id: 'owner',
  name: 'Proprietário',
  email: 'owner@example.test',
  isActive: true,
  roles: [1],
  establishmentIds: ['unit'],
  isCurrentUser: false
}
const stubs = {
  QCheckbox: {
    props: ['label', 'modelValue'],
    template:
      '<button @click="$emit(\'update:modelValue\', !modelValue)">{{ label }}</button>'
  },
  QBtn: {
    props: ['label'],
    template: '<button @click="$emit(\'click\')">{{ label }}</button>'
  },
  QChip: { template: '<span><slot /></span>' },
  QBanner: { template: '<div><slot /></div>' },
  QPage: { template: '<main><slot /></main>' },
  QCard: { template: '<section><slot /></section>' },
  QCardSection: { template: '<div><slot /></div>' },
  QCardActions: { template: '<div><slot /></div>' },
  QDialog: {
    props: ['modelValue'],
    template: '<div v-if="modelValue"><slot /></div>'
  },
  QForm: {
    template: '<form @submit.prevent="$emit(\'submit\')"><slot /></form>'
  },
  QInput: true,
  QSelect: true,
  QPagination: true,
  QSpinner: true,
  QMarkupTable: { template: '<table><slot /></table>' }
}

describe('proteção visual de Owner', () => {
  it('nega Admin e autoalteração de Owner, permitindo outro Owner e plataforma', () => {
    expect(canManageOwner(owner, false, false)).toBe(false)
    expect(canManageOwner({ ...owner, isCurrentUser: true }, true, false)).toBe(
      false
    )
    expect(canManageOwner(owner, true, false)).toBe(true)
    expect(canManageOwner(owner, false, true)).toBe(true)
  })
  it('não oferece ações de Owner a Admin, inclusive para Owner inativo', () => {
    const wrapper = mount(UserPermissions, {
      props: {
        user: { ...owner, isActive: false },
        ownership: false,
        platform: false,
        unitId: 'unit',
        busy: false
      },
      global: { stubs }
    })
    const labels = wrapper.findAll('button').map((button) => button.text())
    expect(labels).not.toContain('Owner')
    expect(labels).not.toContain('Ativar usuário')
    expect(wrapper.text()).toContain('Owner — protegido')
  })
  it('outro Owner pode solicitar alteração sem gravar diretamente', async () => {
    const wrapper = mount(UserPermissions, {
      props: {
        user: owner,
        ownership: true,
        platform: false,
        unitId: 'unit',
        busy: false
      },
      global: { stubs }
    })
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Owner')!
      .trigger('click')
    expect(wrapper.emitted('role')).toEqual([[1, false]])
  })
})

describe('fluxo de usuários', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    setActivePinia(createPinia())
    const session = useSessionStore()
    session.context = {
      passwordChangeRequired: false,
      isPlatformUser: false,
      capabilities: ['administration', 'ownership'],
      establishments: [{ id: 'unit', name: 'Unidade' }]
    }
    session.unitId = 'unit'
    vi.mocked(usersClient.search).mockResolvedValue({
      items: [owner],
      totalCount: 1,
      page: 1,
      pageSize: 20
    })
  })
  it('cancelar confirmação não envia alteração', async () => {
    const wrapper = mount(UsersPage, { global: { stubs } })
    await flushPromises()
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Gerenciar')!
      .trigger('click')
    wrapper.findComponent(UserPermissions).vm.$emit('active', false)
    await flushPromises()
    expect(wrapper.text()).toContain('Confirmar alteração')
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Cancelar')!
      .trigger('click')
    expect(usersClient.active).not.toHaveBeenCalled()
    wrapper.unmount()
  })
  it('mostra conflito do servidor e mantém editor aberto', async () => {
    vi.mocked(usersClient.active).mockRejectedValue(
      new ApiError({ detail: 'Último Owner ativo.', status: 409 })
    )
    const wrapper = mount(UsersPage, { global: { stubs } })
    await flushPromises()
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Gerenciar')!
      .trigger('click')
    wrapper.findComponent(UserPermissions).vm.$emit('active', false)
    await flushPromises()
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Confirmar')!
      .trigger('click')
    await flushPromises()
    expect(usersClient.active).toHaveBeenCalledWith('unit', 'owner', false)
    expect(wrapper.text()).toContain('Último Owner ativo.')
    expect(wrapper.findComponent(UserPermissions).exists()).toBe(true)
    wrapper.unmount()
  })
  it('impede envio duplicado enquanto o cadastro está em andamento', async () => {
    let complete!: () => void
    vi.mocked(usersClient.create).mockImplementation(
      () => new Promise<void>((resolve) => (complete = resolve))
    )
    const wrapper = mount(UsersPage, { global: { stubs } })
    await flushPromises()
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Novo usuário')!
      .trigger('click')
    const form = wrapper.findAll('form')[1]!
    await form.trigger('submit')
    await form.trigger('submit')
    expect(usersClient.create).toHaveBeenCalledTimes(1)
    complete()
    await flushPromises()
    wrapper.unmount()
  })
  it('renderiza ausência de resultados', async () => {
    vi.mocked(usersClient.search).mockResolvedValue({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20
    })
    const wrapper = mount(UsersPage, { global: { stubs } })
    await flushPromises()
    expect(wrapper.text()).toContain('Nenhum usuário encontrado')
    wrapper.unmount()
  })
})
