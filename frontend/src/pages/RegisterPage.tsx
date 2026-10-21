import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { ApiError, errorMessage } from '../api/errors'
import { useAuth } from '../auth/useAuth'
import { Button } from '../components/ui/Button'
import { TextField } from '../components/ui/TextField'
import { AuthLayout } from './AuthLayout'

type FieldErrors = Record<string, string[]>

export function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setSubmitting(true)
    setError(null)
    setFieldErrors({})
    try {
      await register({
        email: String(form.get('email')),
        userName: String(form.get('userName')),
        password: String(form.get('password')),
      })
      navigate('/', { replace: true })
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
        setFieldErrors(err.fieldErrors)
      } else if (err instanceof ApiError && err.code === 'email_taken') {
        setFieldErrors({ email: [err.message] })
      } else if (err instanceof ApiError && err.code === 'username_taken') {
        setFieldErrors({ userName: [err.message] })
      } else {
        setError(errorMessage(err))
      }
      setSubmitting(false)
    }
  }

  return (
    <AuthLayout
      title="Create your account"
      error={error}
      onSubmit={handleSubmit}
      footer={
        <>
          Already have an account? <Link to="/login">Sign in</Link>
        </>
      }
    >
      <TextField
        label="Email"
        name="email"
        type="email"
        autoComplete="email"
        required
        autoFocus
        errors={fieldErrors.email}
      />
      <TextField
        label="Username"
        name="userName"
        autoComplete="username"
        required
        minLength={3}
        maxLength={32}
        errors={fieldErrors.userName}
      />
      <TextField
        label="Password"
        name="password"
        type="password"
        autoComplete="new-password"
        required
        minLength={8}
        errors={fieldErrors.password}
      />
      <Button type="submit" disabled={submitting}>
        {submitting ? 'Creating account…' : 'Create account'}
      </Button>
    </AuthLayout>
  )
}
