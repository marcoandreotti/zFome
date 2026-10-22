import { createRouter, createMemoryHistory } from 'vue-router'
import { describe, it, expect, vi } from 'vitest'
import { routes } from '../src/router/routes'
import { installAccessGuard } from '../src/router/access'
import type { SessionContext } from '../src/modules/session/store'

describe('navegação com rotas lazy', () => {
  it('hidrata antes de entrar na administração e preserva a fundação', async () => {
    const router = createRouter({ history: createMemoryHistory(), routes })
    const session = {
      context: null as SessionContext | null,
      hydrate: vi.fn(async () => {
        session.context = {
          passwordChangeRequired: false,
          isPlatformUser: false,
          capabilities: ['management'],
          establishments: []
        }
      }),
      clear: vi.fn()
    }
    installAccessGuard(router, session)
    await router.push('/administration')
    expect(session.hydrate).toHaveBeenCalledOnce()
    expect(router.currentRoute.value.path).toBe('/administration')
    await router.push('/administration/foundation')
    expect(router.currentRoute.value.path).toBe('/administration/foundation')
  })
  it('limpa o estado e redireciona quando a sessão expirou', async () => {
    const router = createRouter({ history: createMemoryHistory(), routes })
    const clear = vi.fn()
    installAccessGuard(router, {
      context: null,
      hydrate: vi.fn().mockRejectedValue(new Error('Expired')),
      clear
    })
    await router.push('/administration')
    expect(router.currentRoute.value.path).toBe('/login')
    expect(clear).toHaveBeenCalledOnce()
  })
  it('nega a navegação direta da cozinha', async () => {
    const router = createRouter({ history: createMemoryHistory(), routes })
    installAccessGuard(router, {
      context: {
        passwordChangeRequired: false,
        isPlatformUser: false,
        capabilities: ['kitchen'],
        establishments: []
      },
      hydrate: vi.fn(),
      clear: vi.fn()
    })
    await router.push('/administration')
    expect(router.currentRoute.value.path).toBe('/access-denied')
  })
  it('protege o painel e permite o percurso operacional da cozinha', async () => {
    const router = createRouter({ history: createMemoryHistory(), routes })
    installAccessGuard(router, {
      context: {
        passwordChangeRequired: false,
        isPlatformUser: false,
        capabilities: ['order-read', 'order-kitchen'],
        establishments: [{ id: 'unit', name: 'Unidade' }]
      },
      hydrate: vi.fn(),
      clear: vi.fn()
    })
    await router.push('/operations')
    expect(router.currentRoute.value.path).toBe('/operations')
    await router.push('/administration')
    expect(router.currentRoute.value.path).toBe('/access-denied')
  })
})
