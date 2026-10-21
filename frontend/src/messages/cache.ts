import type { InfiniteData, QueryClient } from '@tanstack/react-query'
import type { ConversationSummary } from '../api/conversations'
import type { Message, MessagePage } from '../api/messages'
import { conversationKeys } from '../conversations/queries'

export type ChatMessage = Message & {
  status?: 'sending' | 'failed'
  error?: string
}

export type ChatPage = Omit<MessagePage, 'items'> & { items: ChatMessage[] }

export const messageKeys = {
  list: (conversationId: string) => ['messages', conversationId] as const,
}

export function upsertMessage(queryClient: QueryClient, message: ChatMessage) {
  queryClient.setQueryData<InfiniteData<ChatPage, string | null>>(
    messageKeys.list(message.conversationId),
    (data) => {
      if (!data) return data
      let found = false
      const pages = data.pages.map((page) => ({
        ...page,
        items: page.items.map((item) => {
          if (item.id !== message.id) return item
          found = true
          return message
        }),
      }))
      if (!found) {
        pages[0] = { ...pages[0], items: [...pages[0].items, message] }
      }
      return { ...data, pages }
    },
  )
}

export function touchConversation(queryClient: QueryClient, message: Message) {
  const list = queryClient.getQueryData<ConversationSummary[]>(conversationKeys.all)
  const existing = list?.find((conversation) => conversation.id === message.conversationId)
  if (!list || !existing) {
    void queryClient.invalidateQueries({ queryKey: conversationKeys.all, exact: true })
    return
  }
  queryClient.setQueryData<ConversationSummary[]>(conversationKeys.all, [
    { ...existing, lastMessageAt: message.sentAt },
    ...list.filter((conversation) => conversation.id !== message.conversationId),
  ])
}
