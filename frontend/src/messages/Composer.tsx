import {
  useRef,
  useState,
  type ClipboardEvent,
  type DragEvent,
  type FormEvent,
  type KeyboardEvent,
} from 'react'
import { useActivity } from '../realtime/useActivity'
import { useRealtime } from '../realtime/useRealtime'
import styles from './Composer.module.css'
import { UploadList } from './UploadList'
import { useUploads } from './useUploads'

const MAX_LENGTH = 4000
const MAX_HEIGHT_PX = 240
const ACCEPT = [
  'image/png',
  'image/jpeg',
  'image/gif',
  'image/webp',
  '.pdf',
  '.zip',
  '.txt',
  '.md',
  '.log',
  '.csv',
  '.json',
  '.yaml',
  '.yml',
  '.sql',
  '.cs',
  '.ts',
  '.tsx',
  '.js',
  '.py',
  '.diff',
  '.patch',
].join(',')

type Props = {
  conversationId: string
  label: string
  parentMessageId?: string
  onEditLast?: () => void
  autoFocus?: boolean
}

function hasFiles(event: DragEvent) {
  return event.dataTransfer.types.includes('Files')
}

export function Composer({ conversationId, label, parentMessageId, onEditLast, autoFocus }: Props) {
  const { sendMessage } = useRealtime()
  const { notifyTyping } = useActivity()
  const [content, setContent] = useState('')
  const [dragging, setDragging] = useState(false)
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const uploads = useUploads(conversationId)
  const hasText = content.trim().length > 0
  const canSend = (hasText || uploads.ready) && !uploads.blocking && content.length <= MAX_LENGTH

  function resize() {
    const textarea = textareaRef.current
    if (!textarea) return
    textarea.style.height = 'auto'
    textarea.style.height = `${Math.min(textarea.scrollHeight, MAX_HEIGHT_PX)}px`
  }

  function submit() {
    if (!canSend) return
    sendMessage(conversationId, content, parentMessageId ?? null, uploads.take())
    setContent('')
    requestAnimationFrame(resize)
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault()
      submit()
    } else if (
      event.key === 'ArrowUp' &&
      content === '' &&
      uploads.uploads.length === 0 &&
      onEditLast
    ) {
      event.preventDefault()
      onEditLast()
    }
  }

  function handlePaste(event: ClipboardEvent<HTMLTextAreaElement>) {
    const files = [...event.clipboardData.files]
    if (files.length === 0) return
    event.preventDefault()
    uploads.add(files)
  }

  function handleDrop(event: DragEvent<HTMLFormElement>) {
    if (!hasFiles(event)) return
    event.preventDefault()
    setDragging(false)
    uploads.add([...event.dataTransfer.files])
    textareaRef.current?.focus()
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    submit()
  }

  return (
    <form
      className={styles.composer}
      onSubmit={handleSubmit}
      data-dragging={dragging}
      onDragOver={(event) => {
        if (!hasFiles(event)) return
        event.preventDefault()
        setDragging(true)
      }}
      onDragLeave={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setDragging(false)
      }}
      onDrop={handleDrop}
    >
      <UploadList uploads={uploads.uploads} onRemove={uploads.remove} />
      <div className={styles.field}>
        <button
          type="button"
          className={styles.attach}
          onClick={() => fileInputRef.current?.click()}
          aria-label="Attach files"
          title="Attach files (or paste / drop them here)"
        >
          +
        </button>
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
          onPaste={handlePaste}
        />
        <input
          ref={fileInputRef}
          type="file"
          multiple
          accept={ACCEPT}
          hidden
          data-testid="file-input"
          onChange={(event) => {
            uploads.add([...(event.target.files ?? [])])
            event.target.value = ''
          }}
        />
      </div>
      <div className={styles.footer}>
        <span className={styles.hint}>
          Markdown supported · Enter to send · Shift+Enter for a new line · ↑ to edit your last
          message · paste or drop files
        </span>
        {uploads.notice && (
          <span className={styles.notice} role="status">
            {uploads.notice}
          </span>
        )}
        {content.length > MAX_LENGTH * 0.9 && (
          <span className={styles.counter} data-over={content.length > MAX_LENGTH}>
            {content.length}/{MAX_LENGTH}
          </span>
        )}
      </div>
    </form>
  )
}
