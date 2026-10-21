import { boot } from 'quasar/wrappers'
import { api, setSessionHooks } from '../http/client'
import { sessionPinia, useSessionStore } from '../modules/session/store'

export { api } from '../http/client'
export type { ProblemDetails } from '../http/client'

export default boot(({ app, router }) => {
  app.use(sessionPinia)
  const session = useSessionStore(sessionPinia)
  setSessionHooks({
    revision: () => session.revision,
    expired: () => {
      session.clear()
      void router.replace('/login')
    }
  })
  app.config.globalProperties.$api = api
})
