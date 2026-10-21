import { useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { useActivity } from '../realtime/useActivity'
import { useRealtime } from '../realtime/useRealtime'
import styles from './Composer.module.css'

const MAX_LENGTH = 4000
const MAX_HEIGHT_PX = 240

type Props = {
  conversationId: string
  label: string
  parentMessageId?: string
  onEditLast?: () => void
  autoFocus?: boolean
}

export function Composer({ conversationId, label, parentMessageId, onEditLast, autoFocus }: Props) {
  const { sendMessage } = useRealtime()
  const { notifyTyping } = useActivity()
  const [content, setContent] = useState('')
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const canSend = content.trim().length > 0 && content.length <= MAX_LENGTH

  function resize() {
    const textarea = textareaRef.current
    if (!textarea) return
    textarea.style.height = 'auto'
    textarea.style.height = `${Math.min(textarea.scrollHeight, MAX_HEIGHT_PX)}px`
  }

  function submit() {
    if (!canSend) return
    sendMessage(conversationId, content, parentMessageId ?? null)
    setContent('')
    requestAnimationFrame(resize)
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault()
      submit()
    } else if (event.key === 'ArrowUp' && content === '' && onEditLast) {
      event.preventDefault()
      onEditLast()
    }
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    submit()
  }

  return (
    <form className={styles.composer} onSubmit={handleSubmit}>
      <textarea
        ref={textareaRef}
        className={styles.input}
        rows={1}
        value={content}
        placeholder={label}
        aria-label={label}
        autoFocus={autoFocus}
        onChange={(event) => {
          setContent(event.target.value)
          if (event.target.value.trim()) notifyTyping(conversationId, parentMessageId ?? null)
          resize()
        }}
        onKeyDown={handleKeyDown}
      />
      <div className={styles.footer}>
        <span className={styles.hint}>
          Markdown supported · Enter to send · Shift+Enter for a new line · ↑ to edit your last
          message
        </span>
        {content.length > MAX_LENGTH * 0.9 && (
          <span className={styles.counter} data-over={content.length > MAX_LENGTH}>
            {content.length}/{MAX_LENGTH}
          </span>
        )}
      </div>
    </form>
  )
}
