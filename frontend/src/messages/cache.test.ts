import { QueryClient, type InfiniteData } from '@tanstack/react-query'
import { beforeEach, describe, expect, it } from 'vitest'
import type { Conversation, ConversationSummary } from '../api/conversations'
import { conversationKeys } from '../conversations/queries'
import {
  applyReceipt,
  lastChangeSeq,
  markReadLocally,
  messageKeys,
  touchConversation,
  UNREAD_CAP,
  upsertMessage,
  type ChatMessage,
  type ChatPage,
} from './cache'

const conversationId = 'c1'

function message(id: string, seq: number, extra: Partial<ChatMessage> = {}): ChatMessage {
  return {
    id,
    conversationId,
    senderId: 'u',
    senderUserName: 'u',
    content: id,
    sentAt: '2026-01-01T00:00:00Z',
    seq,
    changeSeq: seq,
    parentMessageId: null,
    replyCount: 0,
    lastReplyAt: null,
    lastReplySeq: null,
    editedAt: null,
    deletedAt: null,
    ...extra,
  }
}

describe('message cache', () => {
  let queryClient: QueryClient

  function seed(...items: ChatMessage[]) {
    queryClient.setQueryData<InfiniteData<ChatPage, string | null>>(
      messageKeys.list(conversationId),
      {
        pages: [{ items, nextCursor: null }],
        pageParams: [null],
      },
    )
  }

  function ids() {
    return queryClient
      .getQueryData<InfiniteData<ChatPage, string | null>>(messageKeys.list(conversationId))!
      .pages[0].items.map((item) => `${item.id}:${item.seq}${item.status ? `:${item.status}` : ''}`)
  }

  beforeEach(() => {
    queryClient = new QueryClient()
  })

  it('keeps messages ordered by sequence with pending ones last', () => {
    seed(message('a', 1), message('p', 0, { status: 'sending' }))

    upsertMessage(queryClient, message('c', 3))
    upsertMessage(queryClient, message('b', 2))

    expect(ids()).toEqual(['a:1', 'b:2', 'c:3', 'p:0:sending'])
  })

  it('replaces a pending message with the server version', () => {
    seed(message('a', 1), message('p', 0, { status: 'sending' }))

    upsertMessage(queryClient, message('p', 2))

    expect(ids()).toEqual(['a:1', 'p:2'])
  })

  it('does not let a late pending update hide the acknowledged message', () => {
    seed(message('p', 2))

    upsertMessage(queryClient, message('p', 0, { status: 'failed' }))

    expect(ids()).toEqual(['p:2'])
  })

  it('ignores older versions of a message', () => {
    seed(message('a', 1, { changeSeq: 5, content: 'edited' }))

    upsertMessage(queryClient, message('a', 1, { changeSeq: 1, content: 'original' }))

    const items = queryClient.getQueryData<InfiniteData<ChatPage, string | null>>(
      messageKeys.list(conversationId),
    )!.pages[0].items
    expect(items[0].content).toBe('edited')
  })

  it('reports the last change position, or null when nothing is cached', () => {
    expect(lastChangeSeq(queryClient, conversationId)).toBeNull()

    seed(message('a', 1), message('b', 4), message('p', 0, { status: 'sending' }))

    expect(lastChangeSeq(queryClient, conversationId)).toBe(4)
  })

  it('does nothing for conversations that are not loaded', () => {
    upsertMessage(queryClient, message('a', 1))

    expect(lastChangeSeq(queryClient, conversationId)).toBeNull()
  })

  it('routes replies to their thread and leaves the main list alone', () => {
    seed(message('parent', 1))
    queryClient.setQueryData<InfiniteData<ChatPage, string | null>>(
      messageKeys.replies(conversationId, 'parent'),
      { pages: [{ items: [], nextCursor: null }], pageParams: [null] },
    )

    upsertMessage(queryClient, message('reply', 2, { parentMessageId: 'parent' }))

    expect(ids()).toEqual(['parent:1'])
    const thread = queryClient.getQueryData<InfiniteData<ChatPage, string | null>>(
      messageKeys.replies(conversationId, 'parent'),
    )!
    expect(thread.pages[0].items.map((item) => item.id)).toEqual(['reply'])
    expect(lastChangeSeq(queryClient, conversationId)).toBe(2)
  })

  it('ignores updates to messages older than the loaded pages', () => {
    queryClient.setQueryData<InfiniteData<ChatPage, string | null>>(
      messageKeys.list(conversationId),
      {
        pages: [{ items: [message('recent', 40)], nextCursor: 'older' }],
        pageParams: [null],
      },
    )

    upsertMessage(queryClient, message('ancient', 3, { changeSeq: 41, content: 'edited' }))

    expect(ids()).toEqual(['recent:40'])
  })

  it('keeps updates to old messages once the whole history is loaded', () => {
    seed(message('recent', 40))

    upsertMessage(queryClient, message('ancient', 3, { changeSeq: 41 }))

    expect(ids()).toEqual(['ancient:3', 'recent:40'])
  })
})

describe('conversation list cache', () => {
  const me = 'me'
  let queryClient: QueryClient

  function summary(id: string, extra: Partial<ConversationSummary> = {}): ConversationSummary {
    return {
      id,
      name: id,
      memberCount: 2,
      myRole: 'Member',
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
      lastMessageAt: null,
      lastSeq: 5,
      lastDeliveredSeq: 5,
      lastReadSeq: 5,
      unreadCount: 0,
      ...extra,
    }
  }

  function seedList(...items: ConversationSummary[]) {
    queryClient.setQueryData(conversationKeys.all, items)
  }

  function list() {
    return queryClient.getQueryData<ConversationSummary[]>(conversationKeys.all)!
  }

  beforeEach(() => {
    queryClient = new QueryClient()
  })

  it('moves the conversation to the top and counts messages from others as unread', () => {
    seedList(summary('other'), summary(conversationId))

    touchConversation(queryClient, message('m6', 6, { sentAt: '2026-01-02T00:00:00Z' }), me)

    expect(list().map((c) => c.id)).toEqual([conversationId, 'other'])
    expect(list()[0]).toMatchObject({
      lastSeq: 6,
      unreadCount: 1,
      lastMessageAt: '2026-01-02T00:00:00Z',
    })
  })

  it('does not count my own messages, thread replies or replays as unread', () => {
    seedList(summary(conversationId))

    touchConversation(queryClient, message('mine', 6, { senderId: me }), me)
    touchConversation(queryClient, message('reply', 7, { parentMessageId: 'mine' }), me)
    touchConversation(queryClient, message('old', 4), me)

    expect(list()[0]).toMatchObject({ lastSeq: 7, unreadCount: 0 })
  })

  it('caps the unread counter', () => {
    seedList(summary(conversationId, { unreadCount: UNREAD_CAP }))

    touchConversation(queryClient, message('m6', 6), me)

    expect(list()[0].unreadCount).toBe(UNREAD_CAP)
  })

  it('marks a conversation read only when the read position reaches its last message', () => {
    seedList(summary(conversationId, { lastSeq: 9, unreadCount: 4 }))

    markReadLocally(queryClient, conversationId, 7)
    expect(list()[0]).toMatchObject({ lastReadSeq: 5, unreadCount: 4 })

    markReadLocally(queryClient, conversationId, 9)
    expect(list()[0]).toMatchObject({ lastReadSeq: 9, lastDeliveredSeq: 9, unreadCount: 0 })
  })

  it('only moves member receipt positions forward', () => {
    const detail: Conversation = {
      id: conversationId,
      name: 'team',
      createdBy: me,
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
      members: [
        {
          userId: 'bob',
          userName: 'bob',
          role: 'Member',
          joinedAt: '2026-01-01T00:00:00Z',
          lastDeliveredSeq: 8,
          lastReadSeq: 6,
        },
      ],
    }
    queryClient.setQueryData(conversationKeys.detail(conversationId), detail)

    applyReceipt(
      queryClient,
      { conversationId, userId: 'bob', lastDeliveredSeq: 7, lastReadSeq: 7 },
      me,
    )

    expect(
      queryClient.getQueryData<Conversation>(conversationKeys.detail(conversationId))!.members[0],
    ).toMatchObject({
      lastDeliveredSeq: 8,
      lastReadSeq: 7,
    })
  })
})
