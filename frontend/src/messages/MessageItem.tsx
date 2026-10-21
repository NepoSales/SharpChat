import { useState } from 'react'
import type { Member } from '../api/conversations'
import { Button } from '../components/ui/Button'
import { Dialog } from '../components/ui/Dialog'
import { formatShortTime } from '../components/ui/format'
import { Markdown } from '../markdown/Markdown'
import { useRealtime } from '../realtime/useRealtime'
import type { ChatMessage } from './cache'
import { MessageEditor } from './MessageEditor'
import styles from './MessageItem.module.css'
import { receiptTag } from './receiptTag'

export type ThreadSummary = {
  hasNewReplies: boolean
  onOpen: () => void
}

type Props = {
  message: ChatMessage
  showHeader: boolean
  otherMembers: Member[] | null
  isMine: boolean
  canModerate: boolean
  editing: boolean
  onEditingChange: (editing: boolean) => void
  thread: ThreadSummary | null
}

export function MessageItem({
  message,
  showHeader,
  otherMembers,
  isMine,
  canModerate,
  editing,
  onEditingChange,
  thread,
}: Props) {
  const { retryMessage, editMessage, deleteMessage } = useRealtime()
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [deleteError, setDeleteError] = useState<string | null>(null)

  const deleted = message.deletedAt !== null
  const pending = message.seq === 0
  const tag = otherMembers && !deleted ? receiptTag(message, otherMembers) : null
  const actions = !deleted && !pending && !editing
  const showThread = thread !== null && message.replyCount > 0

  async function confirmDelete() {
    const failure = await deleteMessage(message)
    if (failure) setDeleteError(failure)
    else setConfirmingDelete(false)
  }

  return (
    <article className={styles.message} data-status={message.status} data-editing={editing}>
      {showHeader && (
        <header className={styles.header}>
          <span className={styles.author}>{message.senderUserName}</span>
          <time className={styles.time} dateTime={message.sentAt}>
            {formatShortTime(message.sentAt)}
          </time>
        </header>
      )}

      {actions && (
        <div className={styles.actions} role="toolbar" aria-label="Message actions">
          {thread && (
            <button className={styles.action} onClick={thread.onOpen}>
              reply
            </button>
          )}
          {isMine && (
            <button className={styles.action} onClick={() => onEditingChange(true)}>
              edit
            </button>
          )}
          {(isMine || canModerate) && (
            <button
              className={`${styles.action} ${styles.danger}`}
              onClick={() => {
                setDeleteError(null)
                setConfirmingDelete(true)
              }}
            >
              delete
            </button>
          )}
        </div>
      )}

      {editing ? (
        <MessageEditor
          initialContent={message.content}
          onSave={(content) => editMessage(message, content)}
          onCancel={() => onEditingChange(false)}
        />
      ) : (
        <div className={styles.body}>
          <div className={styles.content}>
            {deleted ? (
              <p className={styles.deleted}>message deleted</p>
            ) : (
              <>
                <Markdown content={message.content} />
                {message.editedAt && (
                  <span
                    className={styles.edited}
                    title={new Date(message.editedAt).toLocaleString()}
                  >
                    (edited)
                  </span>
                )}
              </>
            )}
          </div>
          {tag && (
            <span
              className={styles.tag}
              data-tone={tag.tone}
              title={
                message.status === 'failed'
                  ? message.error
                  : tag.readers.length > 0
                    ? `Read by ${tag.readers.join(', ')}`
                    : undefined
              }
            >
              [{tag.label}]
              {message.status === 'failed' && (
                <button className={styles.retry} onClick={() => retryMessage(message)}>
                  retry
                </button>
              )}
            </span>
          )}
        </div>
      )}

      {showThread && (
        <button className={styles.thread} data-new={thread.hasNewReplies} onClick={thread.onOpen}>
          {message.replyCount} {message.replyCount === 1 ? 'reply' : 'replies'}
          {message.lastReplyAt && <> · last {formatShortTime(message.lastReplyAt)}</>}
          {thread.hasNewReplies && <span className={styles.newReplies}> · new</span>}
        </button>
      )}

      <Dialog
        open={confirmingDelete}
        title="Delete message?"
        onClose={() => setConfirmingDelete(false)}
      >
        <p className={styles.confirmText}>
          The message is removed for everyone. Replies in its thread stay.
        </p>
        {deleteError && (
          <p className={styles.confirmError} role="alert">
            {deleteError}
          </p>
        )}
        <div className={styles.confirmActions}>
          <Button variant="ghost" onClick={() => setConfirmingDelete(false)}>
            Cancel
          </Button>
          <Button className={styles.confirmDelete} onClick={() => void confirmDelete()}>
            Delete
          </Button>
        </div>
      </Dialog>
    </article>
  )
}
