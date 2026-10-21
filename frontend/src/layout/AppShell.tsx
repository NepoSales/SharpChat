import { Outlet, useParams } from 'react-router'
import styles from './AppShell.module.css'
import { Sidebar } from './Sidebar'

export function AppShell() {
  const { conversationId } = useParams()

  return (
    <div className={styles.shell} data-conversation-open={conversationId !== undefined}>
      <Sidebar className={styles.sidebar} />
      <main className={styles.main}>
        <Outlet />
      </main>
    </div>
  )
}
