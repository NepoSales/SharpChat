import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useAuth } from '../auth/useAuth'
import { ActivityContext, type TypingUser } from './ActivityContext'
import { TYPING_SEND_INTERVAL_MS, TYPING_VISIBLE_MS, typingKey } from './typing'
import { useRealtime } from './useRealtime'

const OFFLINE_GRACE_MS = 5_000

type TypingEntry = TypingUser & { expiresAt: number }

export function ActivityProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  const { on, getOnlineMembers, sendTyping } = useRealtime()
  const [online, setOnline] = useState<ReadonlySet<string>>(new Set())
  const [typing, setTyping] = useState<ReadonlyMap<string, TypingEntry[]>>(new Map())
  const offlineTimersRef = useRef(new Map<string, number>())
  const lastSentRef = useRef(new Map<string, number>())
  const loadedRef = useRef(new Set<string>())

  function markOnline(userId: string) {
    window.clearTimeout(offlineTimersRef.current.get(userId))
    offlineTimersRef.current.delete(userId)
    setOnline((current) => (current.has(userId) ? current : new Set(current).add(userId)))
  }

  function markOffline(userId: string, immediately: boolean) {
    const remove = () => {
      offlineTimersRef.current.delete(userId)
      setOnline((current) => {
        if (!current.has(userId)) return current
        const next = new Set(current)
        next.delete(userId)
        return next
      })
    }
    window.clearTimeout(offlineTimersRef.current.get(userId))
    if (immediately) remove()
    else offlineTimersRef.current.set(userId, window.setTimeout(remove, OFFLINE_GRACE_MS))
  }

  function clearTyping(key: string, userId: string) {
    setTyping((current) => {
      const entries = current.get(key)
      if (!entries?.some((entry) => entry.userId === userId)) return current
      const next = new Map(current)
      next.set(
        key,
        entries.filter((entry) => entry.userId !== userId),
      )
      return next
    })
  }

  async function loadPresence(conversationId: string) {
    loadedRef.current.add(conversationId)
    const ids = await getOnlineMembers(conversationId)
    ids?.forEach(markOnline)
  }

  useEffect(() => {
    const offPresence = on('presenceChanged', (presence) => {
      if (presence.isOnline) markOnline(presence.userId)
      else markOffline(presence.userId, false)
    })
    const offTyping = on('userTyping', (event) => {
      const key = typingKey(event.conversationId, event.parentMessageId)
      const expiresAt = Date.now() + TYPING_VISIBLE_MS
      setTyping((current) => {
        const next = new Map(current)
        const others = (current.get(key) ?? []).filter((entry) => entry.userId !== event.userId)
        next.set(key, [...others, { userId: event.userId, userName: event.userName, expiresAt }])
        return next
      })
    })
    const offMessage = on('messageReceived', (message) => {
      clearTyping(typingKey(message.conversationId, message.parentMessageId), message.senderId)
    })
    const offConnected = on('connected', () => {
      loadedRef.current.forEach((conversationId) => void loadPresence(conversationId))
    })
    return () => {
      offPresence()
      offTyping()
      offMessage()
      offConnected()
    }
  })

  const hasTyping = [...typing.values()].some((entries) => entries.length > 0)
  useEffect(() => {
    if (!hasTyping) return
    const timer = window.setInterval(() => {
      const now = Date.now()
      setTyping((current) => {
        let changed = false
        const next = new Map<string, TypingEntry[]>()
        current.forEach((entries, key) => {
          const alive = entries.filter((entry) => entry.expiresAt > now)
          if (alive.length !== entries.length) changed = true
          if (alive.length > 0) next.set(key, alive)
        })
        return changed ? next : current
      })
    }, 1_000)
    return () => window.clearInterval(timer)
  }, [hasTyping])

  function notifyTyping(conversationId: string, parentMessageId: string | null) {
    const key = typingKey(conversationId, parentMessageId)
    const now = Date.now()
    if (now - (lastSentRef.current.get(key) ?? 0) < TYPING_SEND_INTERVAL_MS) return
    lastSentRef.current.set(key, now)
    sendTyping(conversationId, parentMessageId)
  }

  function typingIn(conversationId: string, parentMessageId: string | null) {
    return (typing.get(typingKey(conversationId, parentMessageId)) ?? []).filter(
      (entry) => entry.userId !== user?.id,
    )
  }

  function isOnline(userId: string) {
    return userId === user?.id || online.has(userId)
  }

  return (
    <ActivityContext
      value={{
        isOnline,
        typingIn,
        notifyTyping,
        loadPresence: (conversationId) => void loadPresence(conversationId),
      }}
    >
      {children}
    </ActivityContext>
  )
}
