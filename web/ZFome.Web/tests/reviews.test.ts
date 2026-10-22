import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useSessionStore } from '../src/modules/session/store'
import ReviewPage from '../src/modules/public-ordering/ReviewPage.vue'
import ReviewsPage from '../src/modules/administration/reviews/ReviewsPage.vue'
import { publicOrderingClient } from '../src/modules/public-ordering/client'
import { reviewsClient, type ReviewRow } from '../src/modules/administration/reviews/client'

const route = vi.hoisted(() => ({ params: { reference: 'a'.repeat(48) } }))
vi.mock('vue-router', () => ({ useRoute: () => route }))
vi.mock('../src/modules/public-ordering/client', () => ({
  publicOrderingClient: { reviewProgress: vi.fn(), context: vi.fn(), submitReviews: vi.fn() }
}))
vi.mock('../src/modules/administration/reviews/client', () => ({
  reviewsClient: { list: vi.fn(), reject: vi.fn(), hide: vi.fn(), approve: vi.fn(), restore: vi.fn() }
}))

const review: ReviewRow = {
  id: 'review-1', productId: null, productName: null, rating: 5, comment: null,
  status: 'Pending', moderationReason: null, createdAt: '2026-10-20T12:00:00Z', updatedAt: '2026-10-20T12:00:00Z'
}

const stubs = {
  QPage: { template: '<main><slot /></main>' },
  QBanner: { template: '<div><slot /></div>' },
  QSpinner: true,
  ProblemBanner: { template: '<div><slot /></div>' },
  QForm: { emits: ['submit'], template: '<form @submit.prevent="$emit(\'submit\', $event)"><slot /></form>' },
  QRating: {
    name: 'QRating', props: ['modelValue'], emits: ['update:modelValue'],
    template: '<button type="button" @click="$emit(\'update:modelValue\', 5)">rate</button>'
  },
  QInput: {
    props: ['label', 'modelValue', 'type'], emits: ['update:modelValue'],
    template: '<label>{{ label }}<textarea v-if="type === \'textarea\'" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" /><input v-else :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" /></label>'
  },
  QBtn: {
    props: ['label', 'type', 'disable'], emits: ['click'],
    template: '<button :type="type || \'button\'" :disabled="disable" @click="$emit(\'click\')">{{ label }}</button>'
  },
  QSelect: {
    props: ['label', 'modelValue', 'options'], emits: ['update:modelValue'],
    template: '<label>{{ label }}<select :value="modelValue" @change="$emit(\'update:modelValue\', $event.target.value)"><option v-for="option in options" :value="option.value">{{ option.label }}</option></select></label>'
  },
  QTable: {
    props: ['rows'],
    template: '<div v-if="rows[0]"><slot name="body-cell-status" :row="rows[0]"/><slot name="body-cell-actions" :row="rows[0]"/></div>'
  },
  QTd: { template: '<div><slot /></div>' },
  QBadge: { template: '<span><slot /></span>' },
  QDialog: { props: ['modelValue'], template: '<div v-if="modelValue"><slot /></div>' },
  QCard: { template: '<section><slot /></section>' },
  QCardSection: { template: '<div><slot /></div>' },
  QCardActions: { template: '<div><slot /></div>' },
  QPagination: { template: '<div />' }
}

beforeEach(() => {
  vi.clearAllMocks()
  sessionStorage.clear()
  localStorage.clear()
  route.params.reference = 'a'.repeat(48)
  setActivePinia(createPinia())
  useSessionStore().unitId = 'unit-1'
  vi.mocked(publicOrderingClient.reviewProgress).mockResolvedValue({
    canReview: true, expiresAt: null, establishmentReviewed: false, pendingApproval: false,
    establishmentName: 'X-Salada', establishmentLogoUrl: null, products: []
  })
  vi.mocked(publicOrderingClient.submitReviews).mockResolvedValue({ submittedAt: '2026-10-20T12:00:00Z', ratingsSubmitted: 1 })
  vi.mocked(reviewsClient.list).mockResolvedValue({ total: 1, items: [review] })
  vi.mocked(reviewsClient.reject).mockResolvedValue(undefined)
})

describe('public review form', () => {
  it('recovers establishment branding from the saved menu and offers navigation', async () => {
    localStorage.setItem('zfome.public-receipt.lanchonete-vila', route.params.reference)
    vi.mocked(publicOrderingClient.reviewProgress).mockResolvedValueOnce({
      canReview: true, expiresAt: null, establishmentReviewed: false, pendingApproval: false,
      establishmentName: 'Estabelecimento', establishmentLogoUrl: null, products: []
    })
    vi.mocked(publicOrderingClient.context).mockResolvedValueOnce({
      establishmentName: 'Lanchonete da Vila', slug: 'lanchonete-vila',
      theme: { primaryColor: '#000', secondaryColor: '#000', backgroundColor: '#fff', textColor: '#111', fontFamily: 'sans', logoUrl: '/logo.png' },
      table: null, paymentMethods: []
    })
    const wrapper = mount(ReviewPage, { global: { stubs, directives: { 'close-popup': {} } } })
    await flushPromises()

    expect(wrapper.text()).toContain('Lanchonete da Vila')
    expect(wrapper.find('img').attributes('src')).toBe('/logo.png')
    expect(wrapper.text()).toContain('Voltar ao cardápio')
    expect(wrapper.text()).toContain('Voltar ao acompanhamento')
  })

  it('reuses the idempotency key after a failed response and retry', async () => {
    vi.mocked(publicOrderingClient.submitReviews)
      .mockRejectedValueOnce(new Error('response lost'))
      .mockResolvedValueOnce({ submittedAt: '2026-10-20T12:00:00Z', ratingsSubmitted: 1 })
    const wrapper = mount(ReviewPage, { global: { stubs, directives: { 'close-popup': {} } } })
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'rate')!.trigger('click')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    const calls = vi.mocked(publicOrderingClient.submitReviews).mock.calls
    expect(calls).toHaveLength(2)
    expect(calls[0]![2]).toBe(calls[1]![2])
    expect(wrapper.text()).toContain('recebida')
    wrapper.unmount()
  })

  it('shows a submitted review as read-only without a cancellation action', async () => {
    vi.mocked(publicOrderingClient.reviewProgress).mockResolvedValueOnce({
      canReview: false, expiresAt: null, establishmentReviewed: true, pendingApproval: true,
      establishmentName: 'X-Salada', establishmentLogoUrl: null, products: [{
        productId: 'product-1', productName: 'Misto Quente', reviewed: true,
        rating: 4, comment: 'Muito bom', status: 'Pending', createdAt: '2026-10-20T12:00:00Z'
      }],
      establishmentReview: { rating: 5, comment: 'Excelente atendimento', status: 'Pending', createdAt: '2026-10-20T12:00:00Z' }
    })
    const wrapper = mount(ReviewPage, { global: { stubs, directives: { 'close-popup': {} } } })
    await flushPromises()

    expect(wrapper.text()).toContain('Excelente atendimento')
    expect(wrapper.text()).toContain('Aguardando análise')
    expect(wrapper.text()).toContain('Muito bom')
    expect(wrapper.text()).not.toContain('Enviar avaliação')
    expect(wrapper.text()).not.toContain('Cancelar avaliação')
    wrapper.unmount()
  })
})

describe('reviews moderation queue', () => {
  it('renders a pending review and sends its approval', async () => {
    const wrapper = mount(ReviewsPage, { global: { stubs, directives: { 'close-popup': {} } } })
    await flushPromises()

    expect(wrapper.text()).toContain('Pendente')
    await wrapper.findAll('button').find(button => button.text() === 'Aprovar e publicar')!.trigger('click')
    await flushPromises()

    expect(reviewsClient.approve).toHaveBeenCalledWith('unit-1', 'review-1')
    wrapper.unmount()
  })

  it('sends a pending rejection to the reject endpoint with its reason', async () => {
    const wrapper = mount(ReviewsPage, { global: { stubs, directives: { 'close-popup': {} } } })
    await flushPromises()
    await wrapper.findAll('button').find(button => button.text() === 'Reprovar')!.trigger('click')
    await wrapper.get('textarea').setValue('Contém dado pessoal')
    await wrapper.findAll('button').filter(button => button.text() === 'Reprovar').at(-1)!.trigger('click')
    await flushPromises()

    expect(reviewsClient.reject).toHaveBeenCalledWith('unit-1', 'review-1', 'Contém dado pessoal')
    expect(reviewsClient.hide).not.toHaveBeenCalled()
    wrapper.unmount()
  })

  it('renders rejected state distinctly without a restore action', async () => {
    vi.mocked(reviewsClient.list).mockResolvedValue({ total: 1, items: [{ ...review, status: 'Rejected', moderationReason: 'Policy' }] })
    const wrapper = mount(ReviewsPage, { global: { stubs, directives: { 'close-popup': {} } } })
    await flushPromises()
    expect(wrapper.text()).toContain('Reprovada')
    expect(wrapper.findAll('button').some(button => button.text() === 'Restaurar e publicar')).toBe(false)
    wrapper.unmount()
  })
})
