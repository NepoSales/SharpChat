import { createContext } from 'react'
import type { ChatMessage } from '../messages/cache'

export type ConnectionState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

export type RealtimeContextValue = {
  state: ConnectionState
  sendMessage: (conversationId: string, content: string) => Promise<void>
  retryMessage: (message: ChatMessage) => Promise<void>
}

export const RealtimeContext = createContext<RealtimeContextValue | null>(null)
