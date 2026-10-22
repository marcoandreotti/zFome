export const terminalOrderStatuses = new Set(['Completed', 'Cancelled', 'Rejected'])
export function pollingDelay(failures: number) {
  return Math.min(5000 * 2 ** Math.max(0, failures), 60000)
}
