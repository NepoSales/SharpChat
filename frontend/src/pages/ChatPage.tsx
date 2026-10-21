import { useAuth } from '../auth/useAuth'
import { Logo } from '../components/brand/Logo'
import { Button } from '../components/ui/Button'

export function ChatPage() {
  const { user, logout } = useAuth()

  return (
    <main style={{ display: 'grid', placeItems: 'center', height: '100%', gap: 'var(--space-4)' }}>
      <Logo size="var(--text-xl)" />
      <p>Signed in as {user?.userName}</p>
      <Button variant="ghost" onClick={() => void logout()}>
        Sign out
      </Button>
    </main>
  )
}
