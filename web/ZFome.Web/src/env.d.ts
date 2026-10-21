/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_OPERATIONS_POLL_INTERVAL_MS?: string
  readonly VITE_API_BASE_URL?: string
  readonly VITE_REALTIME_BASE_URL?: string
}
