import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { ApiError, errorMessage } from '../api/errors'
import { Button } from '../components/ui/Button'
import { Dialog } from '../components/ui/Dialog'
import { TextField } from '../components/ui/TextField'
import styles from './forms.module.css'
import { useCreateConversation } from './queries'

type Props = {
  open: boolean
  onClose: () => void
}

export function NewConversationDialog({ open, onClose }: Props) {
  const navigate = useNavigate()
  const create = useCreateConversation()
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})

  function close() {
    setError(null)
    setFieldErrors({})
    onClose()
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const memberUserNames = String(form.get('members'))
      .split(/[\s,]+/)
      .map((name) => name.replace(/^@/, ''))
      .filter(Boolean)

    setError(null)
    setFieldErrors({})
    try {
      const conversation = await create.mutateAsync({
        name: String(form.get('name')).trim(),
        memberUserNames,
      })
      close()
      navigate(`/c/${conversation.id}`)
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
        setFieldErrors(err.fieldErrors)
      } else {
        setError(errorMessage(err))
      }
    }
  }

  return (
    <Dialog open={open} title="New conversation" onClose={close}>
      <form className={styles.form} onSubmit={handleSubmit} noValidate>
        {error && (
          <p className={styles.error} role="alert">
            {error}
          </p>
        )}
        <TextField
          label="Name"
          name="name"
          required
          maxLength={100}
          autoFocus
          placeholder="e.g. backend-team"
          errors={fieldErrors.name}
        />
        <TextField
          label="Members (optional)"
          name="members"
          placeholder="usernames, separated by spaces or commas"
          autoComplete="off"
          errors={fieldErrors.memberUserNames}
        />
        <div className={styles.actions}>
          <Button type="button" variant="ghost" onClick={close}>
            Cancel
          </Button>
          <Button type="submit" disabled={create.isPending}>
            {create.isPending ? 'Creating…' : 'Create'}
          </Button>
        </div>
      </form>
    </Dialog>
  )
}
