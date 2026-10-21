import { useState } from 'react'
import { Link, NavLink } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { Logo } from '../components/brand/Logo'
import { Button } from '../components/ui/Button'
import { formatShortTime } from '../components/ui/format'
import { NewConversationDialog } from '../conversations/NewConversationDialog'
import { useConversations } from '../conversations/queries'
import { UNREAD_CAP } from '../messages/cache'
import styles from './Sidebar.module.css'

export function Sidebar({ className }: { className?: string }) {
  const { user, logout } = useAuth()
  const conversations = useConversations()
  const [creating, setCreating] = useState(false)

  return (
    <aside className={[styles.sidebar, className].filter(Boolean).join(' ')}>
      <header className={styles.header}>
        <Link to="/" className={styles.home}>
          <Logo />
        </Link>
        <Button
          variant="ghost"
          className={styles.newButton}
          onClick={() => setCreating(true)}
          aria-label="New conversation"
          title="New conversation"
        >
          +
        </Button>
      </header>

      <nav className={styles.list} aria-label="Conversations">
        {conversations.isPending && <p className={styles.hint}>Loading…</p>}
        {conversations.isError && (
          <p className={styles.hint}>
            Couldn't load conversations.{' '}
            <button className={styles.linkButton} onClick={() => void conversations.refetch()}>
              Retry
            </button>
          </p>
        )}
        {conversations.data?.length === 0 && (
          <p className={styles.hint}>No conversations yet. Create one with +.</p>
        )}
        {conversations.data?.map((conversation) => (
          <NavLink
            key={conversation.id}
            to={`/c/${conversation.id}`}
            className={({ isActive }) =>
              [styles.item, isActive && styles.active].filter(Boolean).join(' ')
            }
          >
            <span className={styles.name}>
              <span className={styles.hash}>#</span>
              {conversation.name}
            </span>
            <span className={styles.meta}>
              {conversation.unreadCount > 0 ? (
                <span className={styles.unread} aria-label={`${conversation.unreadCount} unread`}>
                  {conversation.unreadCount >= UNREAD_CAP ? '99+' : conversation.unreadCount}
                </span>
              ) : conversation.lastMessageAt ? (
                formatShortTime(conversation.lastMessageAt)
              ) : (
                ''
              )}
            </span>
          </NavLink>
        ))}
      </nav>

      <footer className={styles.footer}>
        <span className={styles.user} title={user?.email}>
          @{user?.userName}
        </span>
        <Button variant="ghost" onClick={() => void logout()}>
          Sign out
        </Button>
      </footer>

      <NewConversationDialog open={creating} onClose={() => setCreating(false)} />
    </aside>
  )
}
