import { Fragment, useEffect, useEffectEvent, useLayoutEffect, useRef, useState } from 'react'
import type { Member } from '../api/conversations'
import { useAuth } from '../auth/useAuth'
import { useRealtime } from '../realtime/useRealtime'
import type { ChatMessage } from './cache'
import { MessageItem } from './MessageItem'
import styles from './MessageList.module.css'
import { useMessages } from './queries'

const GROUP_WINDOW_MS = 5 * 60_000
const LOAD_MORE_THRESHOLD_PX = 200
const NEAR_BOTTOM_PX = 120

type Props = {
  conversationId: string
  members: Member[]
  unreadOnOpen: { readSeq: number; lastSeq: number } | undefined
}

export function MessageList({ conversationId, members, unreadOnOpen }: Props) {
  const [unreadRange] = useState(unreadOnOpen)
  const { user } = useAuth()
  const { markRead } = useRealtime()
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
  const lastSeq = messages.reduce((max, message) => Math.max(max, message.seq), 0)
  const firstUnreadId = unreadRange
    ? messages.find(
        (message) =>
          message.seq > unreadRange.readSeq &&
          message.seq <= unreadRange.lastSeq &&
          message.senderId !== user?.id,
      )?.id
    : undefined
  const otherMembers = members.filter((member) => member.userId !== user?.id)
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = history

  function markReadIfSeen() {
    if (document.visibilityState !== 'visible' || !document.hasFocus()) return
    if (!nearBottomRef.current) return
    markRead(conversationId, lastSeq)
  }

  const tryMarkRead = useEffectEvent(markReadIfSeen)

  useEffect(() => {
    tryMarkRead()
    window.addEventListener('focus', tryMarkRead)
    document.addEventListener('visibilitychange', tryMarkRead)
    return () => {
      window.removeEventListener('focus', tryMarkRead)
      document.removeEventListener('visibilitychange', tryMarkRead)
    }
  }, [lastSeq])

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
    markReadIfSeen()
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
        <Fragment key={message.id}>
          {message.id === firstUnreadId && (
            <div className={styles.unreadDivider} role="separator">
              <span>new messages</span>
            </div>
          )}
          <MessageItem
            message={message}
            showHeader={message.id === firstUnreadId || startsGroup(messages[index - 1], message)}
            otherMembers={message.senderId === user?.id ? otherMembers : null}
          />
        </Fragment>
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
