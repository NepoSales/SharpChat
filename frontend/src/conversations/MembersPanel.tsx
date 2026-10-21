import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import type { Conversation, ConversationRole, Member } from '../api/conversations'
import { errorMessage } from '../api/errors'
import { useAuth } from '../auth/useAuth'
import { Button } from '../components/ui/Button'
import { TextField } from '../components/ui/TextField'
import formStyles from './forms.module.css'
import styles from './MembersPanel.module.css'
import { conversationKeys, useAddMember, useChangeRole, useRemoveMember } from './queries'
import { canChangeRoles, canManageMembers, canRemove } from './roles'

type Props = {
  conversation: Conversation
  onClose: () => void
}

export function MembersPanel({ conversation, onClose }: Props) {
  const { user } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const addMember = useAddMember(conversation.id)
  const changeRole = useChangeRole(conversation.id)
  const removeMember = useRemoveMember(conversation.id)
  const [error, setError] = useState<string | null>(null)

  const me = conversation.members.find((member) => member.userId === user?.id)
  const myRole: ConversationRole = me?.role ?? 'Member'

  async function run(action: () => Promise<unknown>) {
    setError(null)
    try {
      await action()
      return true
    } catch (err) {
      setError(errorMessage(err))
      return false
    }
  }

  async function handleAdd(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const userName = String(new FormData(form).get('userName')).trim().replace(/^@/, '')
    if (!userName) return
    if (await run(() => addMember.mutateAsync(userName))) form.reset()
  }

  async function handleLeave() {
    if (!me || !window.confirm(`Leave #${conversation.name}?`)) return
    const left = await run(() => removeMember.mutateAsync(me.userId))
    if (left) {
      queryClient.removeQueries({ queryKey: conversationKeys.detail(conversation.id) })
      navigate('/', { replace: true })
    }
  }

  function handleRemove(member: Member) {
    if (window.confirm(`Remove @${member.userName} from #${conversation.name}?`)) {
      void run(() => removeMember.mutateAsync(member.userId))
    }
  }

  return (
    <aside className={styles.panel} aria-label="Members">
      <header className={styles.header}>
        <h2 className={styles.title}>Members · {conversation.members.length}</h2>
        <Button variant="ghost" onClick={onClose} aria-label="Close members">
          ✕
        </Button>
      </header>

      {error && (
        <p className={formStyles.error} role="alert">
          {error}
        </p>
      )}

      {canManageMembers(myRole) && (
        <form className={formStyles.inline} onSubmit={handleAdd}>
          <TextField label="Add member" name="userName" placeholder="username" autoComplete="off" />
          <Button type="submit" disabled={addMember.isPending}>
            Add
          </Button>
        </form>
      )}

      <ul className={styles.list}>
        {conversation.members.map((member) => (
          <li key={member.userId} className={styles.member}>
            <span className={styles.userName}>
              @{member.userName}
              {member.userId === user?.id && <span className={styles.you}> (you)</span>}
            </span>
            <span className={styles.controls}>
              {canChangeRoles(myRole) && member.role !== 'Owner' ? (
                <select
                  className={styles.role}
                  value={member.role}
                  aria-label={`Role of ${member.userName}`}
                  disabled={changeRole.isPending}
                  onChange={(event) =>
                    void run(() =>
                      changeRole.mutateAsync({
                        userId: member.userId,
                        role: event.target.value as ConversationRole,
                      }),
                    )
                  }
                >
                  <option value="Admin">Admin</option>
                  <option value="Member">Member</option>
                </select>
              ) : (
                <span className={styles.badge} data-role={member.role}>
                  {member.role}
                </span>
              )}
              {member.userId !== user?.id && canRemove(myRole, member.role) && (
                <button
                  className={styles.remove}
                  onClick={() => handleRemove(member)}
                  aria-label={`Remove ${member.userName}`}
                  title="Remove"
                >
                  ✕
                </button>
              )}
            </span>
          </li>
        ))}
      </ul>

      {myRole !== 'Owner' && (
        <Button variant="ghost" className={styles.leave} onClick={() => void handleLeave()}>
          Leave conversation
        </Button>
      )}
    </aside>
  )
}
