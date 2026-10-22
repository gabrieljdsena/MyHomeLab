import { type FormEvent, useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { AuthShell } from './AuthShell'
import { authErrorMessage, authInputClass } from './authHelpers'
import { useAuthStatus, useSetup } from './useAuth'

export function SetupPage() {
  const status = useAuthStatus()
  const navigate = useNavigate()
  const setupMutation = useSetup()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')

  if (status.data && !status.data.needsSetup) {
    return <Navigate to="/login" replace />
  }

  const usernameOk = /^[a-zA-Z0-9._-]{3,32}$/.test(username.trim())
  const passwordOk = password.length >= 12
  const confirmOk = password === confirm
  const canSubmit = usernameOk && passwordOk && confirmOk && !setupMutation.isPending

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    if (!canSubmit) return

    setupMutation.mutate(
      { username: username.trim(), password },
      { onSuccess: () => navigate('/', { replace: true }) },
    )
  }

  return (
    <AuthShell title="Create your account" subtitle="First run: pick the credentials that protect this hub.">
      <form className="mt-5 flex flex-col gap-3" onSubmit={handleSubmit}>
        <label className="flex flex-col gap-1.5 text-sm">
          <span className="text-muted">Username</span>
          <input
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            autoComplete="username"
            autoFocus
            required
            className={authInputClass}
          />
          <span className="text-xs text-muted/70">3-32 characters, letters, digits, dot, dash or underscore.</span>
        </label>
        <label className="flex flex-col gap-1.5 text-sm">
          <span className="text-muted">Password</span>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="new-password"
            required
            className={authInputClass}
          />
          <span className="text-xs text-muted/70">At least 12 characters.</span>
        </label>
        <label className="flex flex-col gap-1.5 text-sm">
          <span className="text-muted">Confirm password</span>
          <input
            type="password"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            autoComplete="new-password"
            required
            className={authInputClass}
          />
          {confirm.length > 0 && !confirmOk && <span className="text-xs text-down">Passwords do not match yet.</span>}
        </label>
        {setupMutation.isError && <p className="text-sm text-down">{authErrorMessage(setupMutation.error)}</p>}
        <button
          type="submit"
          disabled={!canSubmit}
          className="mt-1 cursor-pointer rounded-lg bg-accent px-3 py-2 text-sm font-medium text-white transition hover:bg-accent/90 disabled:cursor-default disabled:opacity-60"
        >
          {setupMutation.isPending ? 'Creating…' : 'Create account'}
        </button>
      </form>
    </AuthShell>
  )
}
