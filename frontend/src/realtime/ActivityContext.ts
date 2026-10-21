import { createContext } from 'react'

export type TypingUser = { userId: string; userName: string }

export type ActivityContextValue = {
  isOnline: (userId: string) => boolean
  typingIn: (conversationId: string, parentMessageId: string | null) => TypingUser[]
  notifyTyping: (conversationId: string, parentMessageId: string | null) => void
  loadPresence: (conversationId: string) => void
}

export const ActivityContext = createContext<ActivityContextValue | null>(null)
