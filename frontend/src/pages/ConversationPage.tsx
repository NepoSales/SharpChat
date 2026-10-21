import { useEffect, useEffectEvent, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { ApiError } from '../api/errors'
import { useAuth } from '../auth/useAuth'
import { MembersPanel } from '../conversations/MembersPanel'
import { useConversation, useConversations } from '../conversations/queries'
import { Composer } from '../messages/Composer'
import { lastEditable } from '../messages/lastEditable'
import { MessageList } from '../messages/MessageList'
import { useMessages } from '../messages/queries'
import { ThreadPanel } from '../messages/ThreadPanel'
import { TypingIndicator } from '../messages/TypingIndicator'
import { useActivity } from '../realtime/useActivity'
import styles from './ConversationPage.module.css'

export function ConversationPage() {
  const { conversationId } = useParams() as { conversationId: string }
  const conversation = useConversation(conversationId)
  const summary = useConversations().data?.find((item) => item.id === conversationId)
  const history = useMessages(conversationId)
  const { user } = useAuth()
  const { isOnline, loadPresence } = useActivity()
  const [searchParams, setSearchParams] = useSearchParams()
  const threadId = searchParams.get('thread')
  const [showMembers, setShowMembers] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [openedThreads, setOpenedThreads] = useState<ReadonlySet<string>>(new Set())

  const loadConversationPresence = useEffectEvent(() => loadPresence(conversationId))
  useEffect(() => {
    loadConversationPresence()
  }, [conversationId])

  function openThread(messageId: string) {
    setShowMembers(false)
    setOpenedThreads((opened) => new Set(opened).add(messageId))
    setSearchParams({ thread: messageId })
  }

  function closeThread() {
    setSearchParams({})
  }

  function toggleMembers() {
    if (!showMembers) closeThread()
    setShowMembers((shown) => !shown)
  }

  if (conversation.isPending) {
    return <p className={styles.state}>Loading…</p>
  }

  if (conversation.isError) {
    const notFound = conversation.error instanceof ApiError && conversation.error.status === 404
    return (
      <div className={styles.state}>
        <p>{notFound ? 'This conversation does not exist.' : "Couldn't load this conversation."}</p>
        <Link to="/">Back to conversations</Link>
      </div>
    )
  }

  const { data } = conversation
  const mainMessages = history.data?.pages.flatMap((page) => page.items)
  const onlineCount = data.members.filter((member) => isOnline(member.userId)).length

  return (
    <div className={styles.page}>
      <section className={styles.chat}>
        <header className={styles.header}>
          <Link to="/" className={styles.back} aria-label="Back to conversations">
            ←
          </Link>
          <h1 className={styles.title}>
            <span className={styles.hash}>#</span>
            {data.name}
          </h1>
          <button
            className={styles.membersButton}
            onClick={toggleMembers}
            aria-expanded={showMembers}
          >
            {data.members.length} {data.members.length === 1 ? 'member' : 'members'}
            <span className={styles.online}> · {onlineCount} online</span>
          </button>
        </header>
        <MessageList
          key={conversationId}
          conversationId={conversationId}
          members={data.members}
          history={history}
          editingId={editingId}
          onEditingChange={setEditingId}
          unreadOnOpen={summary && { readSeq: summary.lastReadSeq, lastSeq: summary.lastSeq }}
          openedThreads={openedThreads}
          onOpenThread={openThread}
          emptyText="No messages yet. Say hi!"
          startText="This is the start of the conversation."
        />
        <TypingIndicator conversationId={conversationId} parentMessageId={null} />
        <Composer
          key={`composer-${conversationId}`}
          conversationId={conversationId}
          label={`Message #${data.name}`}
          onEditLast={() => setEditingId(lastEditable(mainMessages, user?.id)?.id ?? null)}
        />
      </section>
      {threadId && (
        <ThreadPanel
          key={threadId}
          conversationId={conversationId}
          parentId={threadId}
          members={data.members}
          onClose={closeThread}
        />
      )}
      {showMembers && !threadId && (
        <MembersPanel conversation={data} onClose={() => setShowMembers(false)} />
      )}
    </div>
  )
}
