import { useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { useRealtime } from '../realtime/useRealtime'
import styles from './Composer.module.css'

const MAX_LENGTH = 4000
const MAX_HEIGHT_PX = 240

export function Composer({ conversationId, name }: { conversationId: string; name: string }) {
  const { state, sendMessage } = useRealtime()
  const [content, setContent] = useState('')
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const connected = state === 'connected'
  const canSend = connected && content.trim().length > 0 && content.length <= MAX_LENGTH

  function resize() {
    const textarea = textareaRef.current
    if (!textarea) return
    textarea.style.height = 'auto'
    textarea.style.height = `${Math.min(textarea.scrollHeight, MAX_HEIGHT_PX)}px`
  }

  function submit() {
    if (!canSend) return
    void sendMessage(conversationId, content)
    setContent('')
    requestAnimationFrame(resize)
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault()
      submit()
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
        placeholder={connected ? `Message #${name}` : 'Connecting…'}
        aria-label={`Message #${name}`}
        onChange={(event) => {
          setContent(event.target.value)
          resize()
        }}
        onKeyDown={handleKeyDown}
      />
      <div className={styles.footer}>
        <span className={styles.hint}>
          Markdown supported · Enter to send, Shift+Enter for a new line
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
