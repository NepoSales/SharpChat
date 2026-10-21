import styles from './Logo.module.css'

type LogoProps = {
  size?: string
  compact?: boolean
}

export function Logo({ size = 'var(--text-lg)', compact = false }: LogoProps) {
  return (
    <span className={styles.logo} style={{ fontSize: size }} role="img" aria-label="SharpChat">
      <span className={styles.hash} aria-hidden="true">
        #
      </span>
      {!compact && <span aria-hidden="true">Chat</span>}
    </span>
  )
}
