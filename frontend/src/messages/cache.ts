import type { InfiniteData, QueryClient } from '@tanstack/react-query'
import type { Conversation, ConversationSummary } from '../api/conversations'
import type { Message, MessagePage, Receipt } from '../api/messages'
import { conversationKeys } from '../conversations/queries'

export type ChatMessage = Message & {
  status?: 'sending' | 'failed'
  error?: string
}

export type ChatPage = Omit<MessagePage, 'items'> & { items: ChatMessage[] }

type MessagesData = InfiniteData<ChatPage, string | null>

export const UNREAD_CAP = 100

export const messageKeys = {
  all: ['messages'] as const,
  list: (conversationId: string) => ['messages', conversationId] as const,
}

function bySeq(a: ChatMessage, b: ChatMessage) {
  const pendingA = a.seq === 0
  const pendingB = b.seq === 0
  if (pendingA !== pendingB) return pendingA ? 1 : -1
  return a.seq - b.seq
}

export function upsertMessage(queryClient: QueryClient, message: ChatMessage) {
  queryClient.setQueryData<MessagesData>(messageKeys.list(message.conversationId), (data) => {
    if (!data) return data
    let found = false
    const pages = data.pages.map((page) => {
      if (!page.items.some((item) => item.id === message.id)) return page
      found = true
      return {
        ...page,
        items: page.items.map((item) =>
          item.id === message.id ? keepNewest(item, message) : item,
        ),
      }
    })
    if (!found) {
      pages[0] = { ...pages[0], items: [...pages[0].items, message].sort(bySeq) }
    } else {
      pages[0] = { ...pages[0], items: [...pages[0].items].sort(bySeq) }
    }
    return { ...data, pages }
  })
}

function keepNewest(current: ChatMessage, incoming: ChatMessage) {
  if (current.seq !== 0 && incoming.seq === 0) return current
  return incoming.changeSeq >= current.changeSeq ? incoming : current
}

export function cachedConversationIds(queryClient: QueryClient) {
  return queryClient
    .getQueriesData<MessagesData>({ queryKey: messageKeys.all })
    .filter(([, data]) => data !== undefined)
    .map(([key]) => key[1] as string)
}

export function lastChangeSeq(queryClient: QueryClient, conversationId: string) {
  const data = queryClient.getQueryData<MessagesData>(messageKeys.list(conversationId))
  if (!data) return null
  return data.pages.reduce(
    (max, page) => page.items.reduce((pageMax, item) => Math.max(pageMax, item.changeSeq), max),
    0,
  )
}

export function touchConversation(queryClient: QueryClient, message: Message, myUserId: string) {
  const list = queryClient.getQueryData<ConversationSummary[]>(conversationKeys.all)
  const existing = list?.find((conversation) => conversation.id === message.conversationId)
  if (!list || !existing) {
    void queryClient.invalidateQueries({ queryKey: conversationKeys.all, exact: true })
    return
  }
  const isNew = message.seq > existing.lastSeq
  const updated: ConversationSummary = {
    ...existing,
    lastMessageAt: isNew ? message.sentAt : existing.lastMessageAt,
    lastSeq: Math.max(existing.lastSeq, message.seq),
    unreadCount:
      isNew && message.senderId !== myUserId && message.seq > existing.lastReadSeq
        ? Math.min(existing.unreadCount + 1, UNREAD_CAP)
        : existing.unreadCount,
  }
  queryClient.setQueryData<ConversationSummary[]>(conversationKeys.all, [
    updated,
    ...list.filter((conversation) => conversation.id !== message.conversationId),
  ])
}

export function applyReceipt(queryClient: QueryClient, receipt: Receipt, myUserId: string) {
  queryClient.setQueryData<Conversation>(conversationKeys.detail(receipt.conversationId), (data) =>
    data
      ? {
          ...data,
          members: data.members.map((member) =>
            member.userId === receipt.userId
              ? {
                  ...member,
                  lastDeliveredSeq: Math.max(member.lastDeliveredSeq, receipt.lastDeliveredSeq),
                  lastReadSeq: Math.max(member.lastReadSeq, receipt.lastReadSeq),
                }
              : member,
          ),
        }
      : data,
  )

  if (receipt.userId === myUserId) {
    markReadLocally(
      queryClient,
      receipt.conversationId,
      receipt.lastReadSeq,
      receipt.lastDeliveredSeq,
    )
  }
}

export function markReadLocally(
  queryClient: QueryClient,
  conversationId: string,
  readSeq: number,
  deliveredSeq = readSeq,
) {
  const list = queryClient.getQueryData<ConversationSummary[]>(conversationKeys.all)
  const existing = list?.find((conversation) => conversation.id === conversationId)
  if (!list || !existing || readSeq <= existing.lastReadSeq) return
  if (readSeq < existing.lastSeq) {
    void queryClient.invalidateQueries({ queryKey: conversationKeys.all, exact: true })
    return
  }
  queryClient.setQueryData<ConversationSummary[]>(
    conversationKeys.all,
    list.map((conversation) =>
      conversation.id === conversationId
        ? {
            ...conversation,
            lastReadSeq: readSeq,
            lastDeliveredSeq: Math.max(conversation.lastDeliveredSeq, deliveredSeq),
            unreadCount: 0,
          }
        : conversation,
    ),
  )
}
