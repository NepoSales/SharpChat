import { Outlet, useParams } from 'react-router'
import { ActivityProvider } from '../realtime/ActivityProvider'
import { RealtimeProvider } from '../realtime/RealtimeProvider'
import styles from './AppShell.module.css'
import { ConnectionBanner } from './ConnectionBanner'
import { Sidebar } from './Sidebar'

export function AppShell() {
  const { conversationId } = useParams()

  return (
    <RealtimeProvider>
      <ActivityProvider>
        <div className={styles.shell} data-conversation-open={conversationId !== undefined}>
          <Sidebar className={styles.sidebar} />
          <main className={styles.main}>
            <ConnectionBanner />
            <Outlet />
          </main>
        </div>
      </ActivityProvider>
    </RealtimeProvider>
  )
}
