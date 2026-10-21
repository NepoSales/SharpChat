import { Logo } from '../brand/Logo'
import styles from './FullPageLoader.module.css'

export function FullPageLoader() {
  return (
    <div className={styles.loader} role="status" aria-label="Loading">
      <Logo size="var(--text-xl)" />
    </div>
  )
}
