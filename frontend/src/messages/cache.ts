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
  conversation: (conversationId: string) => ['messages', conversationId] as const,
  list: (conversationId: string) => ['messages', conversationId, 'list'] as const,
  replies: (conversationId: string, parentId: string) =>
    ['messages', conversationId, 'replies', parentId] as const,
  single: (conversationId: string, messageId: string) =>
    ['messages', conversationId, 'single', messageId] as const,
}

function bySeq(a: ChatMessage, b: ChatMessage) {
  const pendingA = a.seq === 0
  const pendingB = b.seq === 0
  if (pendingA !== pendingB) return pendingA ? 1 : -1
  return a.seq - b.seq
}

function keyFor(message: Message) {
  return message.parentMessageId
    ? messageKeys.replies(message.conversationId, message.parentMessageId)
    : messageKeys.list(message.conversationId)
}

function canHold(data: MessagesData, message: ChatMessage) {
  if (message.seq === 0 || data.pages.at(-1)?.nextCursor === null) return true
  const oldest = data.pages.at(-1)?.items.find((item) => item.seq !== 0)?.seq
  return oldest === undefined || message.seq > oldest
}

export function upsertMessage(queryClient: QueryClient, message: ChatMessage) {
  queryClient.setQueryData<MessagesData>(keyFor(message), (data) => {
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
    if (!found && !canHold(data, message)) return data
    pages[0] = {
      ...pages[0],
      items: (found ? [...pages[0].items] : [...pages[0].items, message]).sort(bySeq),
    }
    return { ...data, pages }
  })

  const singleKey = messageKeys.single(message.conversationId, message.id)
  const single = queryClient.getQueryData<ChatMessage>(singleKey)
  if (single) queryClient.setQueryData(singleKey, keepNewest(single, message))
}

function keepNewest(current: ChatMessage, incoming: ChatMessage) {
  if (current.seq !== 0 && incoming.seq === 0) return current
  return incoming.changeSeq >= current.changeSeq ? incoming : current
}

export function findMessage(queryClient: QueryClient, conversationId: string, messageId: string) {
  const data = queryClient.getQueryData<MessagesData>(messageKeys.list(conversationId))
  for (const page of data?.pages ?? []) {
    const match = page.items.find((item) => item.id === messageId)
    if (match) return match
  }
  return queryClient.getQueryData<ChatMessage>(messageKeys.single(conversationId, messageId))
}

export function cachedConversationIds(queryClient: QueryClient) {
  return queryClient
    .getQueriesData<MessagesData>({ queryKey: messageKeys.all })
    .filter(([key, data]) => key[2] === 'list' && data !== undefined)
    .map(([key]) => key[1] as string)
}

export function lastChangeSeq(queryClient: QueryClient, conversationId: string) {
  const caches = queryClient
    .getQueriesData<MessagesData>({ queryKey: messageKeys.conversation(conversationId) })
    .filter(([key, data]) => key[2] !== 'single' && data !== undefined)
  if (!caches.some(([key]) => key[2] === 'list')) return null
  return caches.reduce(
    (max, [, data]) =>
      data!.pages.reduce(
        (pagesMax, page) =>
          page.items.reduce((itemsMax, item) => Math.max(itemsMax, item.changeSeq), pagesMax),
        max,
      ),
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
  const countsAsUnread =
    isNew &&
    message.parentMessageId === null &&
    message.senderId !== myUserId &&
    message.seq > existing.lastReadSeq
  const updated: ConversationSummary = {
    ...existing,
    lastMessageAt: isNew ? message.sentAt : existing.lastMessageAt,
    lastSeq: Math.max(existing.lastSeq, message.seq),
    unreadCount: countsAsUnread
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
