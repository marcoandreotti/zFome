import { createPinia, defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { api } from '../../http/client'

export interface SessionContext {
  passwordChangeRequired: boolean
  isPlatformUser: boolean
  capabilities: string[]
  establishments: { id: string; name: string }[]
}
export const sessionPinia = createPinia()
export const useSessionStore = defineStore('session', () => {
  const context = ref<SessionContext | null>(null)
  const unitId = ref('')
  const revision = ref(0)
  let loading: Promise<void> | undefined
  const units = computed(() => context.value?.establishments ?? [])
  const can = (capability: string) =>
    !context.value?.passwordChangeRequired &&
    !!context.value?.capabilities.includes(capability)
  function clear() {
    context.value = null
    unitId.value = ''
    revision.value++
    sessionStorage.removeItem('zf-unit')
  }
  function selectUnit(id: string) {
    if (!units.value.some((unit) => unit.id === id))
      throw new Error('Unidade não autorizada.')
    if (id !== unitId.value) {
      unitId.value = id
      revision.value++
    }
    sessionStorage.setItem('zf-unit', id)
  }
  async function hydrate() {
    loading ??= (async () => {
      const current = revision.value
      const response = await api.get<SessionContext>('/api/auth/context')
      if (current !== revision.value) return
      context.value = response.data
      const preferred = unitId.value || sessionStorage.getItem('zf-unit')
      const chosen =
        units.value.find((unit) => unit.id === preferred) ?? units.value[0]
      if (chosen) selectUnit(chosen.id)
      else if (unitId.value) {
        unitId.value = ''
        revision.value++
        sessionStorage.removeItem('zf-unit')
      }
    })().finally(() => {
      loading = undefined
    })
    await loading
  }
  async function logout() {
    try {
      await api.post('/api/auth/logout')
    } finally {
      clear()
    }
  }
  return {
    context,
    unitId,
    units,
    revision,
    can,
    clear,
    selectUnit,
    hydrate,
    logout
  }
})
