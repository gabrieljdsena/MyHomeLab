import { type FormEvent, useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { AuthShell } from './AuthShell'
import { authErrorMessage, authInputClass } from './authHelpers'
import { useAuthStatus, useLogin, useMe } from './useAuth'

export function LoginPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const status = useAuthStatus()
  const me = useMe(true)
  const loginMutation = useLogin()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')

  const from = (location.state as { from?: string } | null)?.from ?? '/'

  if (me.data) {
    return <Navigate to={from} replace />
  }

  if (status.data?.needsSetup) {
    return <Navigate to="/setup" replace />
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    loginMutation.mutate(
      { username, password },
      { onSuccess: () => navigate(from, { replace: true }) },
    )
  }

  return (
    <AuthShell title="Sign in" subtitle="This hub is private. Sign in to continue.">
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
        </label>
        <label className="flex flex-col gap-1.5 text-sm">
          <span className="text-muted">Password</span>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            autoComplete="current-password"
            required
            className={authInputClass}
          />
        </label>
        {loginMutation.isError && <p className="text-sm text-down">{authErrorMessage(loginMutation.error)}</p>}
        <button
          type="submit"
          disabled={loginMutation.isPending}
          className="mt-1 cursor-pointer rounded-lg bg-accent px-3 py-2 text-sm font-medium text-white transition hover:bg-accent/90 disabled:cursor-default disabled:opacity-60"
        >
          {loginMutation.isPending ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </AuthShell>
  )
}
