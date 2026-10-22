import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import OnboardingPage from '../src/modules/administration/onboarding/OnboardingPage.vue'
import { onboardingClient, tableIntent } from '../src/modules/administration/onboarding/client'
import { useSessionStore } from '../src/modules/session/store'
import { adminStubs } from './admin-stubs'

const route = vi.hoisted(() => ({ params: { step: 'dados' } }))
const push = vi.hoisted(() => vi.fn())
vi.mock('vue-router', () => ({ useRoute: () => route, useRouter: () => ({ push }) }))
vi.mock('../src/modules/administration/onboarding/client', async original => ({
  ...(await original<typeof import('../src/modules/administration/onboarding/client')>()),
  onboardingClient: {
    progress: vi.fn(), configuration: vi.fn(), tables: vi.fn(), data: vi.fn(), theme: vi.fn(),
    hours: vi.fn(), createTable: vi.fn(), updateTable: vi.fn(), rotate: vi.fn(), complete: vi.fn()
  }
}))
const stubs = { ...adminStubs, AccessStep: { template: '<div>Acessos</div>' } }
const configuration = () => ({
  tradeName: 'Unidade', slug: 'unidade',
  theme: { primaryColor: null, secondaryColor: null, backgroundColor: null, textColor: null, fontFamily: null, logoUrl: null, faviconUrl: null },
  hours: [] as { dayOfWeek: number; opensAt: string; closesAt: string }[]
})
const progress = (ready = false) => ({
  dataReady: true, themeReady: true, hoursReady: ready, accessReady: true,
  activeTables: 0, completedAt: null, isReady: ready, pendingSteps: ready ? [] : ['horarios']
})
beforeEach(() => {
  vi.clearAllMocks()
  sessionStorage.clear()
  setActivePinia(createPinia())
  useSessionStore().unitId = 'unit'
  route.params.step = 'dados'
  vi.mocked(onboardingClient.configuration).mockImplementation(async () => configuration())
  vi.mocked(onboardingClient.progress).mockImplementation(async () => progress())
  vi.mocked(onboardingClient.tables).mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 20 })
  vi.mocked(onboardingClient.createTable).mockResolvedValue({ id: 'table' })
  vi.mocked(onboardingClient.complete).mockResolvedValue()
})
it('retoma a etapa pela rota e bloqueia conclusão quando a prontidão regrediu', async () => {
  route.params.step = 'revisao'
  const wrapper = mount(OnboardingPage, { global: { stubs } })
  await flushPromises()
  expect(wrapper.text()).toContain('Adicione um horário válido')
  expect(wrapper.findAll('button').find(button => button.text() === 'Concluir configuração')!.attributes('disabled')).toBeDefined()
  expect(onboardingClient.complete).not.toHaveBeenCalled()
  wrapper.unmount()
  vi.mocked(onboardingClient.progress).mockImplementation(async () => progress(true))
  const resumed = mount(OnboardingPage, { global: { stubs } })
  await flushPromises()
  await resumed.findAll('button').find(button => button.text() === 'Concluir configuração')!.trigger('click')
  await flushPromises()
  expect(onboardingClient.complete).toHaveBeenCalledOnce()
  resumed.unmount()
})
it('reutiliza a intenção de criar mesa após uma falha de comunicação', async () => {
  route.params.step = 'mesas'
  const wrapper = mount(OnboardingPage, { global: { stubs } })
  await flushPromises()
  await wrapper.find('input').setValue('A1')
  vi.mocked(onboardingClient.createTable).mockRejectedValueOnce(new Error('connection lost'))
  await wrapper.find('form').trigger('submit')
  await flushPromises()
  const first = vi.mocked(onboardingClient.createTable).mock.calls[0]![1]
  expect(tableIntent('unit').intentId).toBe(first.intentId)
  await wrapper.find('form').trigger('submit')
  await flushPromises()
  expect(vi.mocked(onboardingClient.createTable).mock.calls[1]![1].intentId).toBe(first.intentId)
  expect(sessionStorage.getItem('onboarding-table:unit')).toBeNull()
  wrapper.unmount()
})
