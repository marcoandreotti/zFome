export interface PollingOptions {
  intervalMs: number
  maxIntervalMs: number
  visibility?: Pick<Document, 'hidden' | 'addEventListener' | 'removeEventListener'>
  setTimer?: typeof setTimeout
  clearTimer?: typeof clearTimeout
}

/** Agenda somente depois do ciclo anterior e nunca mantém duas consultas ativas. */
export class PollingCoordinator {
  private timer: ReturnType<typeof setTimeout> | undefined
  private inFlight: Promise<void> | undefined
  private running = false
  private failures = 0
  private readonly visibility
  private readonly setTimer
  private readonly clearTimer
  private readonly onVisibility = () => {
    if (!this.visibility?.hidden) void this.refresh()
    else this.cancelTimer()
  }

  constructor(
    private readonly run: () => Promise<void>,
    private readonly options: PollingOptions
  ) {
    this.visibility = options.visibility
    this.setTimer = options.setTimer ?? setTimeout
    this.clearTimer = options.clearTimer ?? clearTimeout
  }

  start() {
    if (this.running) return
    this.running = true
    this.visibility?.addEventListener('visibilitychange', this.onVisibility)
    if (!this.visibility?.hidden) void this.refresh()
  }

  stop() {
    this.running = false
    this.cancelTimer()
    this.visibility?.removeEventListener('visibilitychange', this.onVisibility)
  }

  refresh(manual = false): Promise<void> {
    if (this.inFlight) return this.inFlight
    if (!this.running && !manual) return Promise.resolve()
    if (this.visibility?.hidden && !manual) return Promise.resolve()
    this.cancelTimer()
    this.inFlight = this.run()
      .then(() => {
        this.failures = 0
      })
      .catch(() => {
        this.failures++
      })
      .finally(() => {
        this.inFlight = undefined
        if (this.running && !this.visibility?.hidden) this.schedule()
      })
    return this.inFlight
  }

  private schedule() {
    const delay = Math.min(
      this.options.maxIntervalMs,
      this.options.intervalMs * 2 ** this.failures
    )
    this.timer = this.setTimer(() => void this.refresh(), delay)
  }

  private cancelTimer() {
    if (this.timer !== undefined) this.clearTimer(this.timer)
    this.timer = undefined
  }
}
