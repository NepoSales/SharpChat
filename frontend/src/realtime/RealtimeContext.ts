import { createContext } from 'react'
import type { ChatMessage } from '../messages/cache'

export type ConnectionState = 'connecting' | 'connected' | 'reconnecting'

export type RealtimeContextValue = {
  state: ConnectionState
  sendMessage: (conversationId: string, content: string) => void
  retryMessage: (message: ChatMessage) => void
  markRead: (conversationId: string, seq: number) => void
}

export const RealtimeContext = createContext<RealtimeContextValue | null>(null)
