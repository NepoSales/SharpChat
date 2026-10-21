import { api } from './client'

export type Message = {
  id: string
  conversationId: string
  senderId: string
  senderUserName: string
  content: string
  sentAt: string
  parentMessageId: string | null
  editedAt: string | null
  deletedAt: string | null
}

export type MessagePage = {
  items: Message[]
  nextCursor: string | null
}

export function getHistory(conversationId: string, before: string | null, signal?: AbortSignal) {
  const query = before ? `?before=${encodeURIComponent(before)}` : ''
  return api<MessagePage>(`/conversations/${conversationId}/messages${query}`, { signal })
}
