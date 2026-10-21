import { api } from './client'
import { setSession, type SessionResponse } from './session'

export type User = {
  id: string
  userName: string
  email: string
}

export type RegisterInput = {
  email: string
  userName: string
  password: string
}

export type LoginInput = {
  email: string
  password: string
}

export async function login(input: LoginInput): Promise<void> {
  const session = await api<SessionResponse>('/auth/login', {
    method: 'POST',
    body: input,
    anonymous: true,
  })
  setSession(session)
}

export function register(input: RegisterInput): Promise<User> {
  return api<User>('/auth/register', { method: 'POST', body: input, anonymous: true })
}

export async function logout(): Promise<void> {
  try {
    await api<void>('/auth/logout', { method: 'POST', anonymous: true })
  } finally {
    setSession(null)
  }
}

export function getMe(signal?: AbortSignal): Promise<User> {
  return api<User>('/users/me', { signal })
}
