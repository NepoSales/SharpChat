import styles from './OnlineDot.module.css'

export function OnlineDot({ online }: { online: boolean }) {
  return (
    <span
      className={styles.dot}
      data-online={online}
      role="img"
      aria-label={online ? 'online' : 'offline'}
    />
  )
}
