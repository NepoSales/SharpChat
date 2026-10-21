import { useEffect, useState } from 'react'
import { useRealtime } from '../realtime/useRealtime'
import styles from './ConnectionBanner.module.css'

const SHOW_AFTER_MS = 1_500

export function ConnectionBanner() {
  const { state } = useRealtime()
  const [visible, setVisible] = useState(false)

  useEffect(() => {
    if (state === 'connected') {
      const hide = window.setTimeout(() => setVisible(false), 0)
      return () => window.clearTimeout(hide)
    }
    const show = window.setTimeout(() => setVisible(true), SHOW_AFTER_MS)
    return () => window.clearTimeout(show)
  }, [state])

  if (!visible || state === 'connected') return null

  return (
    <div className={styles.banner} role="status">
      {state === 'connecting' ? 'Connecting to the chat server…' : 'Connection lost. Reconnecting…'}{' '}
      <span className={styles.hint}>Messages you send now will go out when it is back.</span>
    </div>
  )
}
