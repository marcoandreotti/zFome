import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import AvailabilityPage from '../src/modules/administration/availability/AvailabilityPage.vue'
import { availabilityClient } from '../src/modules/administration/availability/client'
import { catalogClient } from '../src/modules/administration/catalog/client'
import { useSessionStore } from '../src/modules/session/store'
import { adminStubs } from './admin-stubs'

vi.mock('../src/modules/administration/availability/client', async original => ({
  ...(await original<typeof import('../src/modules/administration/availability/client')>()),
  availabilityClient: {
    configuration: vi.fn(), timeZone: vi.fn(), exceptions: vi.fn(),
    scheduling: vi.fn(), saveScheduling: vi.fn(),
    pause: vi.fn(), resume: vi.fn(), unavailable: vi.fn(), reactivate: vi.fn()
  }
}))
vi.mock('../src/modules/administration/catalog/client', () => ({
  catalogClient: { get: vi.fn() }
}))

const stubs = {
  ...adminStubs,
  QList: { template: '<div><slot /></div>' },
  QItem: { template: '<div><slot /></div>' },
  QItemSection: { template: '<div><slot /></div>' }
}

beforeEach(() => {
  vi.clearAllMocks()
  setActivePinia(createPinia())
  useSessionStore().unitId = 'unit'
  vi.mocked(availabilityClient.configuration).mockResolvedValue({
    tradeName: 'Unit', slug: 'unit', timeZoneId: 'America/Sao_Paulo',
    exceptions: [], pauses: []
  })
  vi.mocked(catalogClient.get).mockResolvedValue({
    establishmentId: 'unit', establishmentName: 'Unit', slug: 'unit', categories: []
  })
  vi.mocked(availabilityClient.scheduling).mockResolvedValue({
    timeZoneId: 'America/Sao_Paulo',
    policies: [
      { serviceType: 'Pickup', isEnabled: false, slotIntervalMinutes: 30, minimumAdvanceMinutes: 60, horizonDays: 30, maximumOrdersPerSlot: null },
      { serviceType: 'Delivery', isEnabled: false, slotIntervalMinutes: 30, minimumAdvanceMinutes: 60, horizonDays: 30, maximumOrdersPerSlot: null }
    ]
  })
  vi.mocked(availabilityClient.pause).mockResolvedValue()
})

it('carrega os controles operacionais e permite pausar uma modalidade', async () => {
  const wrapper = mount(AvailabilityPage, { global: { stubs } })
  await flushPromises()
  expect(wrapper.text()).toContain('Exceções de calendário')
  expect(wrapper.text()).toContain('Pausas por modalidade')
  expect(wrapper.text()).toContain('Indisponibilidade de oferta')
  await wrapper.findAll('button').find(button => button.text() === 'Pausar')!.trigger('click')
  await flushPromises()
  expect(availabilityClient.pause).toHaveBeenCalledWith('unit', 'Pickup', null, '')
  wrapper.unmount()
})

it('mostra o rótulo em português para exceções sem alterar o valor técnico da modalidade', async () => {
  vi.mocked(availabilityClient.configuration).mockResolvedValue({
    tradeName: 'Unit', slug: 'unit', timeZoneId: 'America/Sao_Paulo',
    exceptions: [{ date: '2026-10-06', serviceType: 'Table', isOpen: false, opensAt: null, closesAt: null, reason: null }],
    pauses: []
  })

  const wrapper = mount(AvailabilityPage, { global: { stubs } })
  await flushPromises()

  expect(wrapper.text()).toContain('2026-10-06 · Mesa · Fechado')
  expect(wrapper.text()).not.toContain('2026-10-06 · Table')
  await wrapper.findAll('button').find(button => button.text() === 'Pausar')!.trigger('click')
  await flushPromises()
  expect(availabilityClient.pause).toHaveBeenCalledWith('unit', 'Pickup', null, '')
  wrapper.unmount()
})
