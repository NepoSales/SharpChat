import { useLayoutEffect, useRef } from 'react'
import { useAuth } from '../auth/useAuth'
import type { ChatMessage } from './cache'
import { MessageItem } from './MessageItem'
import styles from './MessageList.module.css'
import { useMessages } from './queries'

const GROUP_WINDOW_MS = 5 * 60_000
const LOAD_MORE_THRESHOLD_PX = 200
const NEAR_BOTTOM_PX = 120

export function MessageList({ conversationId }: { conversationId: string }) {
  const { user } = useAuth()
  const history = useMessages(conversationId)
  const scrollRef = useRef<HTMLDivElement>(null)
  const nearBottomRef = useRef(true)
  const previousRef = useRef<{ first?: string; last?: string; scrollHeight: number }>({
    scrollHeight: 0,
  })

  const messages: ChatMessage[] = history.data
    ? [...history.data.pages].reverse().flatMap((page) => page.items)
    : []
  const firstId = messages.at(0)?.id
  const lastMessage = messages.at(-1)
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = history

  useLayoutEffect(() => {
    const element = scrollRef.current
    if (!element) return
    const previous = previousRef.current
    const olderPageLoaded = previous.first !== undefined && firstId !== previous.first
    const newMessageAtBottom = lastMessage?.id !== previous.last

    if (olderPageLoaded && !newMessageAtBottom) {
      element.scrollTop += element.scrollHeight - previous.scrollHeight
    } else if (
      newMessageAtBottom &&
      (nearBottomRef.current || lastMessage?.senderId === user?.id)
    ) {
      element.scrollTop = element.scrollHeight
    }

    previousRef.current = {
      first: firstId,
      last: lastMessage?.id,
      scrollHeight: element.scrollHeight,
    }

    if (element.scrollHeight <= element.clientHeight && hasNextPage && !isFetchingNextPage) {
      void fetchNextPage()
    }
  }, [firstId, lastMessage, user?.id, hasNextPage, isFetchingNextPage, fetchNextPage])

  function handleScroll() {
    const element = scrollRef.current
    if (!element) return
    nearBottomRef.current =
      element.scrollHeight - element.scrollTop - element.clientHeight < NEAR_BOTTOM_PX
    if (element.scrollTop < LOAD_MORE_THRESHOLD_PX && hasNextPage && !isFetchingNextPage) {
      void fetchNextPage()
    }
  }

  return (
    <div ref={scrollRef} className={styles.list} onScroll={handleScroll} aria-live="polite">
      {history.isPending && <p className={styles.hint}>Loading messages…</p>}
      {history.isError && <p className={styles.hint}>Couldn't load messages.</p>}
      {isFetchingNextPage && <p className={styles.hint}>Loading older messages…</p>}
      {history.isSuccess && !hasNextPage && (
        <p className={styles.hint}>
          {messages.length === 0
            ? 'No messages yet. Say hi!'
            : 'This is the start of the conversation.'}
        </p>
      )}
      {messages.map((message, index) => (
        <MessageItem
          key={message.id}
          message={message}
          showHeader={startsGroup(messages[index - 1], message)}
        />
      ))}
    </div>
  )
}

function startsGroup(previous: ChatMessage | undefined, message: ChatMessage) {
  return (
    !previous ||
    previous.senderId !== message.senderId ||
    Date.parse(message.sentAt) - Date.parse(previous.sentAt) > GROUP_WINDOW_MS
  )
}
