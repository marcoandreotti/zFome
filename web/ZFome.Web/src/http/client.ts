import axios, {
  AxiosError,
  CanceledError,
  type AxiosRequestConfig
} from 'axios'

export interface ProblemDetails {
  title?: string
  status?: number
  detail?: string
  traceId?: string
  reason?: string
  errors?: Record<string, string[]>
  currentDeliveryFee?: number
  currentTotal?: number
}
export class ApiError extends Error {
  constructor(public readonly problem: ProblemDetails) {
    super(
      problem.detail ??
        problem.title ??
        (
          {
            400: 'Revise os campos informados e tente novamente.',
            401: 'Sua sessão expirou. Entre novamente.',
            403: 'Você não tem permissão para esta operação na unidade selecionada.',
            404: 'O recurso não foi encontrado. Atualize a listagem.',
            409: 'Os dados estão em conflito. Revise as alterações e tente novamente.'
          } as Record<number, string>
        )[problem.status ?? 0] ??
        'Não foi possível conectar. Tente novamente.'
    )
  }
  field(name: string): string | undefined {
    return Object.entries(this.problem.errors ?? {})
      .find(([key]) => key.toLowerCase() === name.toLowerCase())?.[1]
      .join(' ')
  }
}
interface SessionHooks {
  revision: () => number
  expired: () => void
}
type Request = AxiosRequestConfig & { retried?: boolean; revision?: number }

/** Uma única renovação atende às requisições que receberam 401 ao mesmo tempo. */
export function createApiClient(
  baseURL: string,
  csrf: () => string,
  hooks: SessionHooks
) {
  const client = axios.create({ baseURL, withCredentials: true })
  const renew = axios.create({ baseURL, withCredentials: true })
  let refreshing: Promise<void> | undefined
  client.interceptors.request.use((config) => {
    const request = config as typeof config & Request
    request.revision ??= hooks.revision()
    if (request.revision !== hooks.revision())
      throw new CanceledError('Contexto alterado')
    config.headers.set('X-Correlation-ID', crypto.randomUUID())
    config.headers.set('X-CSRF-Token', csrf())
    return config
  })
  client.interceptors.response.use(
    (response) => {
      if ((response.config as Request).revision !== hooks.revision())
        throw new CanceledError('Contexto alterado')
      return response
    },
    async (error: AxiosError<ProblemDetails>) => {
      if (axios.isCancel(error)) throw error
      const config = error.config as Request | undefined
      if (config && config.revision !== hooks.revision())
        throw new CanceledError('Contexto alterado')
      const status = error.response?.status
      const loginRequest =
        /\/auth\/(begin|complete|refresh|logout|change-password)$/.test(
          config?.url ?? ''
        )
      if (status === 401 && config && !loginRequest) {
        if (!config.retried && csrf()) {
          config.retried = true
          try {
            refreshing ??= renew
              .post('/api/auth/refresh', undefined, {
                headers: { 'X-CSRF-Token': csrf() }
              })
              .then(() => undefined)
              .finally(() => {
                refreshing = undefined
              })
            await refreshing
            if (config.revision !== hooks.revision())
              throw new CanceledError('Contexto alterado')
          } catch (failure) {
            if (axios.isCancel(failure)) throw failure
            if (config.revision === hooks.revision()) hooks.expired()
            throw new ApiError({
              status: 401,
              title: 'Sua sessão expirou. Entre novamente.'
            })
          }
          return client.request(config)
        }
        hooks.expired()
      }
      throw new ApiError({ ...error.response?.data, status })
    }
  )
  return { client, renew }
}
export function readCsrfCookie(): string {
  const value = document.cookie
    .split('; ')
    .find((cookie) => cookie.startsWith('zf_csrf='))
    ?.slice(8)
  return value ? decodeURIComponent(value) : ''
}
let hooks: SessionHooks = { revision: () => 0, expired: () => undefined }
export function setSessionHooks(value: SessionHooks) {
  hooks = value
}
export const { client: api } = createApiClient(
  import.meta.env.VITE_API_BASE_URL ?? '',
  readCsrfCookie,
  {
    revision: () => hooks.revision(),
    expired: () => hooks.expired()
  }
)
