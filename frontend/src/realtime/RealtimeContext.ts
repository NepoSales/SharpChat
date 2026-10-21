import { createContext } from 'react'
import type { Message } from '../api/messages'
import type { ChatMessage } from '../messages/cache'

export type ConnectionState = 'connecting' | 'connected' | 'reconnecting'

export type Presence = { userId: string; isOnline: boolean }

export type Typing = {
  conversationId: string
  parentMessageId: string | null
  userId: string
  userName: string
}

export type RealtimeEventMap = {
  connected: undefined
  messageReceived: Message
  presenceChanged: Presence
  userTyping: Typing
}

export type RealtimeEvent = keyof RealtimeEventMap

export type RealtimeContextValue = {
  state: ConnectionState
  sendMessage: (conversationId: string, content: string, parentMessageId?: string | null) => void
  retryMessage: (message: ChatMessage) => void
  editMessage: (message: ChatMessage, content: string) => Promise<string | null>
  deleteMessage: (message: ChatMessage) => Promise<string | null>
  markRead: (conversationId: string, seq: number) => void
  on: <E extends RealtimeEvent>(
    event: E,
    listener: (payload: RealtimeEventMap[E]) => void,
  ) => () => void
  getOnlineMembers: (conversationId: string) => Promise<string[] | null>
  sendTyping: (conversationId: string, parentMessageId: string | null) => void
}

export const RealtimeContext = createContext<RealtimeContextValue | null>(null)
