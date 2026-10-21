import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useSessionStore } from '../src/modules/session/store'
import CouponsPage from '../src/modules/administration/coupons/CouponsPage.vue'
import PaymentMethodsPage from '../src/modules/administration/payment-methods/PaymentMethodsPage.vue'
import {
  couponsClient,
  couponPayload,
  localDateTime,
  type Coupon
} from '../src/modules/administration/coupons/client'
import { paymentMethodsClient } from '../src/modules/administration/payment-methods/client'
import { ApiError } from '../src/http/client'
import { adminStubs as stubs } from './admin-stubs'
vi.mock('../src/modules/administration/coupons/client', async (original) => ({
  ...(await original<
    typeof import('../src/modules/administration/coupons/client')
  >()),
  couponsClient: { search: vi.fn(), save: vi.fn(), active: vi.fn() }
}))
vi.mock('../src/modules/administration/payment-methods/client', () => ({
  paymentMethodsClient: { search: vi.fn(), save: vi.fn(), active: vi.fn() }
}))
const coupon: Coupon = {
  id: 'coupon',
  code: 'SAVE10',
  description: null,
  discountType: 'Percentage',
  value: 10,
  minimumOrder: 0,
  startsAt: '2026-09-03T12:30:00Z',
  endsAt: '2026-09-04T12:30:00Z',
  maximumUses: null,
  usedCount: 5,
  isActive: true
}
beforeEach(() => {
  vi.clearAllMocks()
  setActivePinia(createPinia())
  useSessionStore().unitId = 'unit'
  vi.mocked(couponsClient.search).mockResolvedValue({
    total: 21,
    items: [coupon]
  })
  vi.mocked(paymentMethodsClient.search).mockResolvedValue({
    total: 21,
    items: [
      {
        id: 'cash',
        code: 'CASH',
        name: 'Dinheiro',
        isOnline: false,
        allowsChange: true,
        isActive: true
      }
    ]
  })
})
it('converte validade no fuso do dispositivo sem enviar estado ou contador de usos', () => {
  const payload = couponPayload(
    coupon,
    localDateTime(coupon.startsAt),
    localDateTime(coupon.endsAt)
  )
  expect(new Date(payload.startsAt).getTime()).toBe(
    new Date(coupon.startsAt).getTime()
  )
  expect(new Date(payload.endsAt).getTime()).toBe(
    new Date(coupon.endsAt).getTime()
  )
  expect(payload.maximumUses).toBeNull()
  expect(payload).not.toHaveProperty('usedCount')
  expect(payload).not.toHaveProperty('isActive')
})
for (const [name, component, client] of [
  ['cupons', CouponsPage, couponsClient],
  ['formas', PaymentMethodsPage, paymentMethodsClient]
] as const) {
  it(`${name}: filtra, pagina e confirma alteração de estado com recarga`, async () => {
    const wrapper = mount(component, { global: { stubs } })
    await flushPromises()
    await wrapper.find('select').setValue('inactive')
    await wrapper.find('form').trigger('submit')
    await flushPromises()
    expect(client.search).toHaveBeenLastCalledWith(
      'unit',
      expect.objectContaining({ isActive: false, page: 1 }),
      expect.any(AbortSignal)
    )
    await wrapper
      .findAll('button')
      .find((x) => x.text() === 'Página 2')!
      .trigger('click')
    await flushPromises()
    expect(client.search).toHaveBeenLastCalledWith(
      'unit',
      expect.objectContaining({ page: 2 }),
      expect.any(AbortSignal)
    )
    await wrapper
      .findAll('button')
      .find((x) => x.text() === 'Desativar')!
      .trigger('click')
    await wrapper
      .findAll('button')
      .find((x) => x.text() === 'Voltar')!
      .trigger('click')
    expect(client.active).not.toHaveBeenCalled()
    await wrapper
      .findAll('button')
      .find((x) => x.text() === 'Desativar')!
      .trigger('click')
    await wrapper
      .findAll('button')
      .find((x) => x.text() === 'Confirmar')!
      .trigger('click')
    await flushPromises()
    expect(client.active).toHaveBeenCalledExactlyOnceWith(
      'unit',
      name === 'cupons' ? 'coupon' : 'cash',
      false
    )
    expect(wrapper.text()).toContain('atualizad')
    wrapper.unmount()
  })
  it(`${name}: conflito não fecha o editor nem perde o código`, async () => {
    const wrapper = mount(component, { global: { stubs } })
    await flushPromises()
    await wrapper
      .findAll('button')
      .find((x) => x.text() === 'Editar')!
      .trigger('click')
    const code = wrapper
      .findAll('label')
      .find((x) => x.text() === 'Código')!
      .find('input')
    await code.setValue('DUPLICADO')
    vi.mocked(client.save).mockRejectedValue(
      new ApiError({ status: 409, detail: 'Código em uso.' })
    )
    await wrapper.findAll('form')[1]!.trigger('submit')
    await wrapper
      .findAll('button')
      .find((x) => x.text() === 'Confirmar')!
      .trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Código em uso.')
    expect((code.element as HTMLInputElement).value).toBe('DUPLICADO')
    expect(client.save).toHaveBeenCalledTimes(1)
    wrapper.unmount()
  })
}
