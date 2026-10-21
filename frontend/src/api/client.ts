import { ApiError } from './errors'
import { API_BASE, getAccessToken, refreshSession } from './session'

type RequestOptions = {
  method?: 'GET' | 'POST' | 'PATCH' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
  anonymous?: boolean
}

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const token = options.anonymous ? null : await getAccessToken()
  let response = await send(path, options, token)

  if (response.status === 401 && !options.anonymous) {
    const refreshed = await refreshSession()
    if (refreshed) {
      response = await send(path, options, refreshed)
    }
  }

  if (!response.ok) {
    throw await ApiError.fromResponse(response)
  }
  if (response.status === 204) {
    return undefined as T
  }
  return (await response.json()) as T
}

function send(path: string, options: RequestOptions, token: string | null) {
  const headers = new Headers({ Accept: 'application/json' })
  if (options.body !== undefined) headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)

  return fetch(`${API_BASE}${path}`, {
    method: options.method ?? 'GET',
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    signal: options.signal,
    credentials: 'same-origin',
  })
}
