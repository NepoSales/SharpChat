import { api } from './client'

export type Message = {
  id: string
  conversationId: string
  senderId: string
  senderUserName: string
  content: string
  sentAt: string
  seq: number
  changeSeq: number
  parentMessageId: string | null
  replyCount: number
  lastReplyAt: string | null
  lastReplySeq: number | null
  editedAt: string | null
  deletedAt: string | null
}

export type MessagePage = {
  items: Message[]
  nextCursor: string | null
}

export type MessageChanges = {
  items: Message[]
  hasMore: boolean
}

export type Receipt = {
  conversationId: string
  userId: string
  lastDeliveredSeq: number
  lastReadSeq: number
}

export function getChanges(conversationId: string, after: number, signal?: AbortSignal) {
  return api<MessageChanges>(`/conversations/${conversationId}/changes?after=${after}`, { signal })
}

function beforeQuery(before: string | null) {
  return before ? `?before=${encodeURIComponent(before)}` : ''
}

export function getHistory(conversationId: string, before: string | null, signal?: AbortSignal) {
  return api<MessagePage>(`/conversations/${conversationId}/messages${beforeQuery(before)}`, {
    signal,
  })
}

export function getReplies(
  conversationId: string,
  messageId: string,
  before: string | null,
  signal?: AbortSignal,
) {
  return api<MessagePage>(
    `/conversations/${conversationId}/messages/${messageId}/replies${beforeQuery(before)}`,
    { signal },
  )
}

export function getMessage(conversationId: string, messageId: string, signal?: AbortSignal) {
  return api<Message>(`/conversations/${conversationId}/messages/${messageId}`, { signal })
}
