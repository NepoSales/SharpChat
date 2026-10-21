import { useEffect, useState, type ReactNode } from 'react'
import * as authApi from '../api/auth'
import { onSessionChange, refreshSession } from '../api/session'
import { AuthContext, type AuthState } from './AuthContext'

const anonymous: AuthState = { status: 'anonymous', user: null }

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ status: 'loading', user: null })

  useEffect(() => {
    const controller = new AbortController()

    refreshSession()
      .then((token) => (token ? authApi.getMe(controller.signal) : null))
      .then((user) => {
        if (!controller.signal.aborted) {
          setState(user ? { status: 'authenticated', user } : anonymous)
        }
      })
      .catch(() => {
        if (!controller.signal.aborted) setState(anonymous)
      })

    const unsubscribe = onSessionChange((signedIn) => {
      if (!signedIn) setState(anonymous)
    })

    return () => {
      controller.abort()
      unsubscribe()
    }
  }, [])

  async function login(input: authApi.LoginInput) {
    await authApi.login(input)
    const user = await authApi.getMe()
    setState({ status: 'authenticated', user })
  }

  async function register(input: authApi.RegisterInput) {
    await authApi.register(input)
    await login({ email: input.email, password: input.password })
  }

  async function logout() {
    await authApi.logout()
    setState(anonymous)
  }

  return <AuthContext value={{ ...state, login, register, logout }}>{children}</AuthContext>
}
