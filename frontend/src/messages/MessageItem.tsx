import type { Member } from '../api/conversations'
import { formatShortTime } from '../components/ui/format'
import { Markdown } from '../markdown/Markdown'
import { useRealtime } from '../realtime/useRealtime'
import type { ChatMessage } from './cache'
import styles from './MessageItem.module.css'
import { receiptTag } from './receiptTag'

type Props = {
  message: ChatMessage
  showHeader: boolean
  otherMembers: Member[] | null
}

export function MessageItem({ message, showHeader, otherMembers }: Props) {
  const { retryMessage } = useRealtime()
  const tag = otherMembers ? receiptTag(message, otherMembers) : null

  return (
    <article className={styles.message} data-status={message.status}>
      {showHeader && (
        <header className={styles.header}>
          <span className={styles.author}>{message.senderUserName}</span>
          <time className={styles.time} dateTime={message.sentAt}>
            {formatShortTime(message.sentAt)}
          </time>
        </header>
      )}
      <div className={styles.body}>
        <div className={styles.content}>
          <Markdown content={message.content} />
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
    </article>
  )
}
