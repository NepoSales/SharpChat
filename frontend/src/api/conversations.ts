import { api } from './client'

export type ConversationRole = 'Owner' | 'Admin' | 'Member'

export type ConversationSummary = {
  id: string
  name: string
  memberCount: number
  myRole: ConversationRole
  createdAt: string
  updatedAt: string
  lastMessageAt: string | null
  lastSeq: number
  lastDeliveredSeq: number
  lastReadSeq: number
  unreadCount: number
}

export type Member = {
  userId: string
  userName: string
  role: ConversationRole
  joinedAt: string
  lastDeliveredSeq: number
  lastReadSeq: number
}

export type Conversation = {
  id: string
  name: string
  createdBy: string
  createdAt: string
  updatedAt: string
  members: Member[]
}

export type CreateConversationInput = {
  name: string
  memberUserNames: string[]
}

export function listConversations(signal?: AbortSignal) {
  return api<ConversationSummary[]>('/conversations', { signal })
}

export function getConversation(id: string, signal?: AbortSignal) {
  return api<Conversation>(`/conversations/${id}`, { signal })
}

export function createConversation(input: CreateConversationInput) {
  return api<Conversation>('/conversations', { method: 'POST', body: input })
}

export function addMember(conversationId: string, userName: string) {
  return api<Member>(`/conversations/${conversationId}/members`, {
    method: 'POST',
    body: { userName },
  })
}

export function changeRole(conversationId: string, userId: string, role: ConversationRole) {
  return api<Member>(`/conversations/${conversationId}/members/${userId}`, {
    method: 'PATCH',
    body: { role },
  })
}

export function removeMember(conversationId: string, userId: string) {
  return api<void>(`/conversations/${conversationId}/members/${userId}`, { method: 'DELETE' })
}
