import { useState } from 'react'
import type { Member } from '../api/conversations'
import { useAuth } from '../auth/useAuth'
import { Composer } from './Composer'
import { MessageItem } from './MessageItem'
import { lastEditable } from './lastEditable'
import { MessageList } from './MessageList'
import { useReplies, useThreadParent } from './queries'
import styles from './ThreadPanel.module.css'

type Props = {
  conversationId: string
  parentId: string
  members: Member[]
  onClose: () => void
}

export function ThreadPanel({ conversationId, parentId, members, onClose }: Props) {
  const { user } = useAuth()
  const { parent, isError } = useThreadParent(conversationId, parentId)
  const replies = useReplies(conversationId, parentId)
  const [editingId, setEditingId] = useState<string | null>(null)

  const replyItems = replies.data?.pages.flatMap((page) => page.items)

  return (
    <aside className={styles.panel} aria-label="Thread">
      <header className={styles.header}>
        <h2 className={styles.title}>Thread</h2>
        <button className={styles.close} onClick={onClose} aria-label="Close thread">
          ✕
        </button>
      </header>

      {isError ? (
        <p className={styles.state}>This thread is not available.</p>
      ) : !parent ? (
        <p className={styles.state}>Loading…</p>
      ) : (
        <>
          <MessageList
            conversationId={conversationId}
            members={members}
            history={replies}
            editingId={editingId}
            onEditingChange={setEditingId}
            emptyText="No replies yet."
            startText={`${parent.replyCount} ${parent.replyCount === 1 ? 'reply' : 'replies'}`}
            leading={
              <div className={styles.parent}>
                <MessageItem
                  message={parent}
                  showHeader
                  otherMembers={null}
                  isMine={parent.senderId === user?.id}
                  canModerate={false}
                  editing={false}
                  onEditingChange={() => undefined}
                  thread={null}
                />
              </div>
            }
          />
          {parent.deletedAt === null && (
            <Composer
              key={parentId}
              conversationId={conversationId}
              parentMessageId={parentId}
              label="Reply in thread"
              autoFocus
              onEditLast={() => setEditingId(lastEditable(replyItems, user?.id)?.id ?? null)}
            />
          )}
        </>
      )}
    </aside>
  )
}
