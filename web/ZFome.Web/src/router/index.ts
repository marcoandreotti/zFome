import { defineRouter } from '#q-app/wrappers'
import { createRouter, createWebHistory } from 'vue-router'
import { routes } from './routes'
import { sessionPinia, useSessionStore } from '../modules/session/store'
import { installAccessGuard } from './access'

export default defineRouter(() => {
  const router = createRouter({
    history: createWebHistory(),
    routes,
    scrollBehavior: () => ({ left: 0, top: 0 })
  })
  installAccessGuard(router, useSessionStore(sessionPinia))
  return router
})
