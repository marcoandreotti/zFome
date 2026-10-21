import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import ProblemBanner from '../src/components/ProblemBanner.vue'
import { ApiError } from '../src/http/client'

describe('mensagens de falha', () => {
  it('apresenta mensagem e referência em região de alerta', () => {
    const wrapper = mount(ProblemBanner, {
      props: {
        error: new ApiError({ title: 'Conflito', traceId: 'request-1' })
      },
      global: { stubs: { QBanner: { template: '<div><slot /></div>' } } }
    })
    expect(wrapper.attributes('role')).toBe('alert')
    expect(wrapper.text()).toContain('Conflito')
    expect(wrapper.text()).toContain('request-1')
  })
  it('não mostra alerta sem erro', () => {
    expect(
      mount(ProblemBanner, {
        props: { error: null },
        global: { stubs: { QBanner: true } }
      }).text()
    ).toBe('')
  })
})
