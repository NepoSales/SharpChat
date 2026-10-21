import { QueryClient, type InfiniteData } from '@tanstack/react-query'
import { beforeEach, describe, expect, it } from 'vitest'
import { lastChangeSeq, messageKeys, upsertMessage, type ChatMessage, type ChatPage } from './cache'

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
