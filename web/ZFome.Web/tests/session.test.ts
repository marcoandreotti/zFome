import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { api, ApiError, createApiClient } from '../src/http/client'
import {
  useSessionStore,
  type SessionContext
} from '../src/modules/session/store'
import { accessDestination, sessionLandingPath } from '../src/router/access'
import {
  AxiosError,
  AxiosHeaders,
  type InternalAxiosRequestConfig
} from 'axios'

const context: SessionContext = {
  passwordChangeRequired: false,
  isPlatformUser: false,
  capabilities: ['management'],
  establishments: [
    { id: 'one', name: 'Unidade 1' },
    { id: 'two', name: 'Unidade 2' }
  ]
}
beforeEach(() => {
  setActivePinia(createPinia())
  sessionStorage.clear()
  vi.restoreAllMocks()
})

describe('sessão e unidade', () => {
  it('aceita somente unidades autorizadas e invalida a revisão ao trocar', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: structuredClone(context) })
    const store = useSessionStore()
    await store.hydrate()
    const revision = store.revision
    store.selectUnit('two')
    expect(store.revision).toBeGreaterThan(revision)
    expect(store.unitId).toBe('two')
    expect(() => store.selectUnit('foreign')).toThrow()
    expect(sessionStorage.getItem('zf-unit')).toBe('two')
    store.clear()
    expect(store.context).toBeNull()
    expect(store.unitId).toBe('')
    expect(sessionStorage.length).toBe(0)
  })
  it('descarta a unidade persistida que não veio do servidor', async () => {
    sessionStorage.setItem('zf-unit', 'foreign')
    vi.spyOn(api, 'get').mockResolvedValue({ data: structuredClone(context) })
    const store = useSessionStore()
    await store.hydrate()
    expect(store.unitId).toBe('one')
  })
  it('não reidrata dados após logout durante uma consulta', async () => {
    let resolve!: (value: unknown) => void
    vi.spyOn(api, 'get').mockImplementation(
      () =>
        new Promise((done) => {
          resolve = done
        })
    )
    const store = useSessionStore()
    const pending = store.hydrate()
    store.clear()
    resolve({ data: context })
    await pending
    expect(store.context).toBeNull()
  })
})

describe('autorização de navegação', () => {
  it('direciona atendimento a clientes sem oferecer gestão não autorizada', () => {
    const attendant = { ...context, capabilities: ['customer-operations'] }
    expect(sessionLandingPath(attendant)).toBe('/administration/customers')
    expect(accessDestination(attendant, '/change-password')).toBe(
      '/administration/customers'
    )
    expect(sessionLandingPath({ ...context, capabilities: ['order-read', 'order-kitchen'] })).toBe('/operations')
  })
  it('manda sessão ausente para o login', () =>
    expect(accessDestination(null, '/administration', 'management')).toBe(
      '/login'
    ))
  it('nega perfil sem gestão mesmo usando URL direta', () =>
    expect(
      accessDestination(
        { ...context, capabilities: ['kitchen'] },
        '/administration',
        'management'
      )
    ).toBe('/access-denied'))
  it('restringe a senha temporária à troca obrigatória', () =>
    expect(
      accessDestination(
        { ...context, passwordChangeRequired: true },
        '/administration'
      )
    ).toBe('/change-password'))
  it('permite gestão a partir das capacidades do servidor', () =>
    expect(
      accessDestination(context, '/administration', 'management')
    ).toBeUndefined())
})

function response(
  config: InternalAxiosRequestConfig,
  status = 200,
  data: unknown = {}
) {
  return {
    config,
    status,
    statusText: String(status),
    headers: new AxiosHeaders(),
    data
  }
}
function unauthorized(config: InternalAxiosRequestConfig) {
  return new AxiosError(
    'Unauthorized',
    'ERR_BAD_REQUEST',
    config,
    undefined,
    response(config, 401)
  )
}

describe('cliente HTTP', () => {
  it('serializa a renovação de várias respostas 401', async () => {
    const expired = vi.fn()
    const { client, renew } = createApiClient('', () => 'csrf', {
      revision: () => 0,
      expired
    })
    let refreshed = false
    const refresh = vi.fn(async (config) => {
      await new Promise((done) => setTimeout(done, 5))
      refreshed = true
      return response(config)
    })
    renew.defaults.adapter = refresh
    client.defaults.adapter = async (config) => {
      if (!refreshed) throw unauthorized(config)
      return response(config, 200, { ok: true })
    }
    const results = await Promise.all([
      client.get('/api/items'),
      client.get('/api/items')
    ])
    expect(refresh).toHaveBeenCalledTimes(1)
    expect(results.every((item) => item.data.ok)).toBe(true)
    expect(expired).not.toHaveBeenCalled()
  })
  it('expira a sessão quando a renovação é recusada', async () => {
    const expired = vi.fn()
    const { client, renew } = createApiClient('', () => 'csrf', {
      revision: () => 0,
      expired
    })
    client.defaults.adapter = async (config) => {
      throw unauthorized(config)
    }
    renew.defaults.adapter = async (config) => {
      throw unauthorized(config)
    }
    await expect(client.get('/api/items')).rejects.toBeInstanceOf(ApiError)
    expect(expired).toHaveBeenCalledOnce()
  })
  it('não transforma uma proibição após renovar em sessão expirada', async () => {
    const expired = vi.fn()
    const { client, renew } = createApiClient('', () => 'csrf', {
      revision: () => 0,
      expired
    })
    let refreshed = false
    renew.defaults.adapter = async (config) => {
      refreshed = true
      return response(config)
    }
    client.defaults.adapter = async (config) => {
      if (!refreshed) throw unauthorized(config)
      throw new AxiosError(
        'Forbidden',
        '',
        config,
        undefined,
        response(config, 403)
      )
    }
    await expect(client.get('/api/items')).rejects.toMatchObject({
      problem: { status: 403 }
    })
    expect(expired).not.toHaveBeenCalled()
  })
  it('descarta respostas da unidade anterior', async () => {
    let revision = 0
    const { client } = createApiClient('', () => '', {
      revision: () => revision,
      expired: vi.fn()
    })
    client.defaults.adapter = async (config) => {
      revision++
      return response(config, 200, { sensitive: true })
    }
    await expect(client.get('/api/items')).rejects.toMatchObject({
      code: 'ERR_CANCELED'
    })
  })
  it('associa erros de campo sem perder o ProblemDetails', () => {
    const error = new ApiError({
      status: 400,
      errors: { Email: ['E-mail inválido'] }
    })
    expect(error.field('email')).toBe('E-mail inválido')
  })
})
