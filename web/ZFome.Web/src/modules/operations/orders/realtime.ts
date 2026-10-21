import {
  HubConnectionBuilder,
  LogLevel,
  type HubConnection
} from '@microsoft/signalr'
import { readCsrfCookie } from '../../../http/client'
import type { PollingCoordinator } from './polling'

export type OrderRealtimeState =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'fallback'

export interface OrderUpdatedMessageV1 {
  version: 1
  orderId: string
  changeType: 'Confirmed' | 'StatusChanged'
  establishmentId: string
  occurredAt: string
}

export interface RealtimeConnection {
  start(): Promise<void>
  stop(): Promise<void>
  invoke(methodName: string, ...args: unknown[]): Promise<unknown>
  on(methodName: string, handler: (message: OrderUpdatedMessageV1) => void): void
  onreconnecting(handler: () => void): void
  onreconnected(handler: () => void): void
  onclose(handler: () => void): void
}

export function createOrderRealtimeConnection(): HubConnection {
  const base = (
    import.meta.env.VITE_REALTIME_BASE_URL ??
    import.meta.env.VITE_API_BASE_URL ??
    (typeof window === 'undefined' ? '' : window.location.origin)
  ).replace(/\/$/, '')
  return new HubConnectionBuilder()
    .withUrl(base + '/hubs/order-updates', {
      withCredentials: true,
      headers: { 'X-CSRF-Token': readCsrfCookie() }
    })
    .withAutomaticReconnect([0, 2_000, 10_000, 30_000])
    .configureLogging(LogLevel.Warning)
    .build()
}

export class OrderRealtimeCoordinator {
  private unitId = ''
  private running = false

  constructor(
    private readonly connection: RealtimeConnection,
    private readonly fallback: Pick<PollingCoordinator, 'start' | 'stop' | 'refresh'>,
    private readonly setState: (state: OrderRealtimeState) => void
  ) {
    connection.on('OrderUpdated', (message) => {
      if (
        this.running &&
        message.version === 1 &&
        message.establishmentId === this.unitId
      )
        void this.fallback.refresh(true)
    })
    connection.onreconnecting(() => {
      if (!this.running) return
      this.setState('reconnecting')
      this.fallback.start()
    })
    connection.onreconnected(() => void this.restore())
    connection.onclose(() => {
      if (!this.running) return
      this.setState('fallback')
      this.fallback.start()
    })
  }

  async start(unitId: string) {
    if (!unitId) {
      await this.stop()
      return
    }
    if (this.running) await this.stop()
    this.unitId = unitId
    this.running = true
    this.setState('connecting')
    this.fallback.start()
    try {
      await this.connection.start()
      await this.connection.invoke('SubscribeAsync', unitId)
      await this.fallback.refresh(true)
      if (!this.running || this.unitId !== unitId) return
      this.fallback.stop()
      this.setState('connected')
    } catch {
      if (!this.running) return
      this.setState('fallback')
      this.fallback.start()
      await this.connection.stop().catch(() => undefined)
    }
  }

  async stop() {
    const wasRunning = this.running
    this.running = false
    this.unitId = ''
    this.fallback.stop()
    this.setState('disconnected')
    if (wasRunning) await this.connection.stop().catch(() => undefined)
  }

  private async restore() {
    const unitId = this.unitId
    if (!this.running || !unitId) return
    try {
      await this.connection.invoke('SubscribeAsync', unitId)
      await this.fallback.refresh(true)
      if (!this.running || this.unitId !== unitId) return
      this.fallback.stop()
      this.setState('connected')
    } catch {
      if (!this.running) return
      this.setState('fallback')
      this.fallback.start()
    }
  }
}
