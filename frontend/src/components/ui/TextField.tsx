import { useId, type InputHTMLAttributes } from 'react'
import styles from './TextField.module.css'

type TextFieldProps = InputHTMLAttributes<HTMLInputElement> & {
  label: string
  errors?: string[]
}

export function TextField({ label, errors, ...props }: TextFieldProps) {
  const id = useId()
  const errorId = `${id}-error`
  const hasErrors = errors !== undefined && errors.length > 0

  return (
    <div className={styles.field}>
      <label className={styles.label} htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        className={styles.input}
        aria-invalid={hasErrors}
        aria-describedby={hasErrors ? errorId : undefined}
        {...props}
      />
      {hasErrors && (
        <p id={errorId} className={styles.error}>
          {errors.join(' ')}
        </p>
      )}
    </div>
  )
}
