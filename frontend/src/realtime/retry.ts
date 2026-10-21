const DELAYS_MS = [0, 2_000, 5_000, 10_000]
const MAX_DELAY_MS = 30_000

export function retryDelay(attempt: number) {
  return DELAYS_MS[attempt] ?? MAX_DELAY_MS
}
