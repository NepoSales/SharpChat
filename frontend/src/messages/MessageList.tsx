import type { InfiniteData, UseInfiniteQueryResult } from '@tanstack/react-query'
import {
  Fragment,
  useEffect,
  useEffectEvent,
  useLayoutEffect,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import type { Member } from '../api/conversations'
import { useAuth } from '../auth/useAuth'
import { canManageMembers } from '../conversations/roles'
import { useRealtime } from '../realtime/useRealtime'
import type { ChatMessage, ChatPage } from './cache'
import { MessageItem } from './MessageItem'
import styles from './MessageList.module.css'

const GROUP_WINDOW_MS = 5 * 60_000
const LOAD_MORE_THRESHOLD_PX = 200
const NEAR_BOTTOM_PX = 120

type UnreadRange = { readSeq: number; lastSeq: number }

type Props = {
  conversationId: string
  members: Member[]
  history: UseInfiniteQueryResult<InfiniteData<ChatPage, unknown>>
  editingId: string | null
  onEditingChange: (messageId: string | null) => void
  unreadOnOpen?: UnreadRange
  openedThreads?: ReadonlySet<string>
  onOpenThread?: (messageId: string) => void
  leading?: ReactNode
  emptyText: string
  startText: string
}

export function MessageList({
  conversationId,
  members,
  history,
  editingId,
  onEditingChange,
  unreadOnOpen,
  openedThreads,
  onOpenThread,
  leading,
  emptyText,
  startText,
}: Props) {
  const [unreadRange] = useState(unreadOnOpen)
  const { user } = useAuth()
  const { markRead } = useRealtime()
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
  const lastSeq = messages.reduce(
    (max, message) => Math.max(max, message.seq, message.lastReplySeq ?? 0),
    0,
  )
  const firstUnreadId = unreadRange
    ? messages.find(
        (message) =>
          message.seq > unreadRange.readSeq &&
          message.seq <= unreadRange.lastSeq &&
          message.senderId !== user?.id,
      )?.id
    : undefined
  const me = members.find((member) => member.userId === user?.id)
  const canModerate = me ? canManageMembers(me.role) : false
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
        <>
          {leading}
          <p className={styles.hint}>{messages.length === 0 ? emptyText : startText}</p>
        </>
      )}
      {messages.map((message, index) => {
        const isMine = message.senderId === user?.id
        return (
          <Fragment key={message.id}>
            {message.id === firstUnreadId && (
              <div className={styles.unreadDivider} role="separator">
                <span>new messages</span>
              </div>
            )}
            <MessageItem
              message={message}
              showHeader={message.id === firstUnreadId || startsGroup(messages[index - 1], message)}
              otherMembers={isMine ? otherMembers : null}
              isMine={isMine}
              canModerate={canModerate}
              editing={editingId === message.id}
              onEditingChange={(editing) => onEditingChange(editing ? message.id : null)}
              thread={
                onOpenThread
                  ? {
                      hasNewReplies:
                        unreadRange !== undefined &&
                        (message.lastReplySeq ?? 0) > unreadRange.readSeq &&
                        !openedThreads?.has(message.id),
                      onOpen: () => onOpenThread(message.id),
                    }
                  : null
              }
            />
          </Fragment>
        )
      })}
    </div>
  )
}

function startsGroup(previous: ChatMessage | undefined, message: ChatMessage) {
  return (
    !previous ||
    previous.senderId !== message.senderId ||
    previous.replyCount > 0 ||
    Date.parse(message.sentAt) - Date.parse(previous.sentAt) > GROUP_WINDOW_MS
  )
}
