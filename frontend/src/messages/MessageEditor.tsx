import { useState, type KeyboardEvent } from 'react'
import { Button } from '../components/ui/Button'
import styles from './MessageEditor.module.css'

const MAX_LENGTH = 4000

type Props = {
  initialContent: string
  onSave: (content: string) => Promise<string | null>
  onCancel: () => void
}

export function MessageEditor({ initialContent, onSave, onCancel }: Props) {
  const [content, setContent] = useState(initialContent)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const canSave = content.trim().length > 0 && content.length <= MAX_LENGTH && !saving

  async function save() {
    if (!canSave) return
    if (content === initialContent) {
      onCancel()
      return
    }
    setSaving(true)
    const failure = await onSave(content)
    setSaving(false)
    if (failure) setError(failure)
    else onCancel()
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Escape') {
      event.preventDefault()
      onCancel()
    } else if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
      event.preventDefault()
      void save()
    }
  }

  return (
    <div className={styles.editor}>
      <textarea
        className={styles.input}
        value={content}
        aria-label="Edit message"
        autoFocus
        rows={Math.min(content.split('\n').length, 10)}
        onFocus={(event) => event.currentTarget.setSelectionRange(content.length, content.length)}
        onChange={(event) => setContent(event.target.value)}
        onKeyDown={handleKeyDown}
      />
      <div className={styles.footer}>
        <span className={styles.hint}>Enter to save · Esc to cancel</span>
        {error && (
          <span className={styles.error} role="alert">
            {error}
          </span>
        )}
        <Button type="button" variant="ghost" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="button" onClick={() => void save()} disabled={!canSave}>
          {saving ? 'Saving…' : 'Save'}
        </Button>
      </div>
    </div>
  )
}
