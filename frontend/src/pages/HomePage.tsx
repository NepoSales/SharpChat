import { Logo } from '../components/brand/Logo'
import styles from './HomePage.module.css'

export function HomePage() {
  return (
    <div className={styles.empty}>
      <Logo size="var(--text-xl)" />
      <p>Pick a conversation on the left, or create one with +.</p>
    </div>
  )
}
