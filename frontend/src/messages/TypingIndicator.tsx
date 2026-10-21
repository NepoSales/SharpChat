import { describeTyping } from '../realtime/typing'
import { useActivity } from '../realtime/useActivity'
import styles from './TypingIndicator.module.css'

type Props = {
  conversationId: string
  parentMessageId: string | null
}

export function TypingIndicator({ conversationId, parentMessageId }: Props) {
  const { typingIn } = useActivity()
  const text = describeTyping(typingIn(conversationId, parentMessageId))

  return (
    <div className={styles.typing} aria-live="polite">
      {text && (
        <>
          <span className={styles.dots} aria-hidden="true">
            <span />
            <span />
            <span />
          </span>
          {text}
        </>
      )}
    </div>
  )
}
