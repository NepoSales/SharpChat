import { ApiError } from './errors'

export const API_BASE = '/api'

export type SessionResponse = {
  accessToken: string
  accessTokenExpiresAt: string
}

type Session = {
  accessToken: string
  expiresAt: number
}

const EXPIRY_MARGIN_MS = 30_000
const REFRESH_LOCK = 'sharpchat-refresh'

let session: Session | null = null
let pendingRefresh: Promise<string | null> | null = null
const listeners = new Set<(signedIn: boolean) => void>()

export function setSession(response: SessionResponse | null) {
  const wasSignedIn = session !== null
  session = response && {
    accessToken: response.accessToken,
    expiresAt: Date.parse(response.accessTokenExpiresAt),
  }
  if (wasSignedIn !== (session !== null)) {
    listeners.forEach((listener) => listener(session !== null))
  }
}

export function onSessionChange(listener: (signedIn: boolean) => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export async function getAccessToken(): Promise<string | null> {
  if (session && session.expiresAt - EXPIRY_MARGIN_MS > Date.now()) {
    return session.accessToken
  }
  return refreshSession()
}

export function refreshSession(): Promise<string | null> {
  pendingRefresh ??= withRefreshLock(requestRefresh).finally(() => {
    pendingRefresh = null
  })
  return pendingRefresh
}

async function requestRefresh(): Promise<string | null> {
  const response = await fetch(`${API_BASE}/auth/refresh`, {
    method: 'POST',
    credentials: 'same-origin',
  })
  if (response.status === 401) {
    setSession(null)
    return null
  }
  if (!response.ok) {
    throw await ApiError.fromResponse(response)
  }
  const body = (await response.json()) as SessionResponse
  setSession(body)
  return body.accessToken
}

function withRefreshLock<T>(task: () => Promise<T>): Promise<T> {
  if (!('locks' in navigator)) return task()
  return navigator.locks.request(REFRESH_LOCK, task)
}
