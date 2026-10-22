import { Dark } from 'quasar'
import { computed, readonly, ref } from 'vue'

export type AppearanceMode = 'system' | 'light' | 'dark'
export type ResolvedAppearance = 'light' | 'dark'

const storageKey = 'zfome.appearance-mode'
const mode = ref<AppearanceMode>('system')
const resolvedMode = ref<ResolvedAppearance>('light')
const isDark = computed(() => resolvedMode.value === 'dark')
let initialized = false
let systemPreference: MediaQueryList | undefined

function isAppearanceMode(value: string | null): value is AppearanceMode {
  return value === 'system' || value === 'light' || value === 'dark'
}

function resolveMode(): ResolvedAppearance {
  if (mode.value === 'light' || mode.value === 'dark') return mode.value
  return systemPreference?.matches ? 'dark' : 'light'
}

function applyAppearance(): void {
  const resolved = resolveMode()
  resolvedMode.value = resolved
  document.documentElement.dataset.theme = resolved
  document.querySelector<HTMLMetaElement>('meta[name="theme-color"]')
    ?.setAttribute('content', resolved === 'dark' ? '#111827' : '#f8fafc')
  Dark.set(resolved === 'dark')
}

function readStoredMode(): AppearanceMode {
  try {
    const stored = window.localStorage.getItem(storageKey)
    return isAppearanceMode(stored) ? stored : 'system'
  } catch {
    return 'system'
  }
}

function onStorage(event: StorageEvent): void {
  if (event.key !== storageKey && event.key !== null) return
  mode.value = isAppearanceMode(event.newValue) ? event.newValue : 'system'
  applyAppearance()
}

export function initializeAppearance(): void {
  if (initialized || typeof window === 'undefined') return

  mode.value = readStoredMode()
  systemPreference = window.matchMedia('(prefers-color-scheme: dark)')
  systemPreference.addEventListener('change', () => {
    if (mode.value === 'system') applyAppearance()
  })
  window.addEventListener('storage', onStorage)
  applyAppearance()
  initialized = true
}

export function setAppearanceMode(next: AppearanceMode): void {
  mode.value = next
  try {
    window.localStorage.setItem(storageKey, next)
  } catch {
    // Keep the selected mode active for this visit if browser storage is unavailable.
  }
  applyAppearance()
}

export function useAppearance() {
  return {
    mode: readonly(mode),
    resolvedMode: readonly(resolvedMode),
    isDark,
    setMode: setAppearanceMode
  }
}
