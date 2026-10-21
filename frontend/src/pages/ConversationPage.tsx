import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { ApiError } from '../api/errors'
import { MembersPanel } from '../conversations/MembersPanel'
import { useConversation, useConversations } from '../conversations/queries'
import { Composer } from '../messages/Composer'
import { MessageList } from '../messages/MessageList'
import styles from './ConversationPage.module.css'

export function ConversationPage() {
  const { conversationId } = useParams() as { conversationId: string }
  const conversation = useConversation(conversationId)
  const summary = useConversations().data?.find((item) => item.id === conversationId)
  const [showMembers, setShowMembers] = useState(false)

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
            onClick={() => setShowMembers((shown) => !shown)}
            aria-expanded={showMembers}
          >
            {data.members.length} {data.members.length === 1 ? 'member' : 'members'}
          </button>
        </header>
        <MessageList
          key={conversationId}
          conversationId={conversationId}
          members={data.members}
          unreadOnOpen={summary && { readSeq: summary.lastReadSeq, lastSeq: summary.lastSeq }}
        />
        <Composer
          key={`composer-${conversationId}`}
          conversationId={conversationId}
          name={data.name}
        />
      </section>
      {showMembers && <MembersPanel conversation={data} onClose={() => setShowMembers(false)} />}
    </div>
  )
}
