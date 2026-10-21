import type { FormEventHandler, ReactNode } from 'react'
import { Logo } from '../components/brand/Logo'
import styles from './AuthLayout.module.css'

type AuthLayoutProps = {
  title: string
  error: string | null
  onSubmit: FormEventHandler<HTMLFormElement>
  footer: ReactNode
  children: ReactNode
}

export function AuthLayout({ title, error, onSubmit, footer, children }: AuthLayoutProps) {
  return (
    <main className={styles.page}>
      <div className={styles.card}>
        <Logo size="var(--text-xl)" />
        <h1 className={styles.title}>{title}</h1>
        <form className={styles.form} onSubmit={onSubmit} noValidate>
          {error && (
            <p className={styles.error} role="alert">
              {error}
            </p>
          )}
          {children}
        </form>
        <p className={styles.footer}>{footer}</p>
      </div>
    </main>
  )
}
