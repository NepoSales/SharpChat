import { createContext } from 'react'
import type { ChatMessage } from '../messages/cache'

export type ConnectionState = 'connecting' | 'connected' | 'reconnecting'

export type RealtimeContextValue = {
  state: ConnectionState
  sendMessage: (conversationId: string, content: string, parentMessageId?: string | null) => void
  retryMessage: (message: ChatMessage) => void
  editMessage: (message: ChatMessage, content: string) => Promise<string | null>
  deleteMessage: (message: ChatMessage) => Promise<string | null>
  markRead: (conversationId: string, seq: number) => void
}

export const RealtimeContext = createContext<RealtimeContextValue | null>(null)
