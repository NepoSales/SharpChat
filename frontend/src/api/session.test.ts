import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

type SessionModule = typeof import('./session')

function sessionResponse(token: string, expiresInMs: number) {
  return {
    accessToken: token,
    accessTokenExpiresAt: new Date(Date.now() + expiresInMs).toISOString(),
  }
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  })
}

describe('session', () => {
  let session: SessionModule
  let fetchMock: ReturnType<typeof vi.fn>

  beforeEach(async () => {
    vi.resetModules()
    fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)
    session = await import('./session')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('returns a valid token without calling the server', async () => {
    session.setSession(sessionResponse('fresh', 10 * 60_000))

    expect(await session.getAccessToken()).toBe('fresh')
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('refreshes a token that is about to expire', async () => {
    session.setSession(sessionResponse('old', 10_000))
    fetchMock.mockResolvedValueOnce(jsonResponse(sessionResponse('new', 15 * 60_000)))

    expect(await session.getAccessToken()).toBe('new')
    expect(fetchMock).toHaveBeenCalledWith('/api/auth/refresh', {
      method: 'POST',
      credentials: 'same-origin',
    })
  })

  it('shares one refresh between concurrent callers', async () => {
    let respond!: (response: Response) => void
    fetchMock.mockReturnValueOnce(new Promise<Response>((resolve) => (respond = resolve)))

    const tokens = Promise.all([
      session.getAccessToken(),
      session.getAccessToken(),
      session.refreshSession(),
    ])
    respond(jsonResponse(sessionResponse('new', 15 * 60_000)))

    expect(await tokens).toEqual(['new', 'new', 'new'])
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('signs out when the refresh token is rejected', async () => {
    const changes: boolean[] = []
    session.onSessionChange((signedIn) => changes.push(signedIn))
    session.setSession(sessionResponse('old', 0))
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 401 }))

    expect(await session.getAccessToken()).toBeNull()
    expect(changes).toEqual([true, false])
  })

  it('keeps the session on server errors and allows a later retry', async () => {
    session.setSession(sessionResponse('old', 0))
    fetchMock
      .mockResolvedValueOnce(jsonResponse({ title: 'boom', status: 500 }, 500))
      .mockResolvedValueOnce(jsonResponse(sessionResponse('new', 15 * 60_000)))

    await expect(session.getAccessToken()).rejects.toThrow()
    expect(await session.getAccessToken()).toBe('new')
  })
})
