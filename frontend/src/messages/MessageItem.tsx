import { formatShortTime } from '../components/ui/format'
import { useRealtime } from '../realtime/useRealtime'
import type { ChatMessage } from './cache'
import styles from './MessageItem.module.css'

type Props = {
  message: ChatMessage
  showHeader: boolean
}

export function MessageItem({ message, showHeader }: Props) {
  const { retryMessage } = useRealtime()

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
      <div className={styles.content}>{message.content}</div>
      {message.status === 'sending' && <p className={styles.status}>Sending…</p>}
      {message.status === 'failed' && (
        <p className={styles.failed}>
          {message.error ?? 'Not sent.'}{' '}
          <button className={styles.retry} onClick={() => void retryMessage(message)}>
            Retry
          </button>
        </p>
      )}
    </article>
  )
}
