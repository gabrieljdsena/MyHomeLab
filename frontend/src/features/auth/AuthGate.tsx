import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { Icon, Spinner } from '../../components/Icon'
import { useAuthStatus, useMe } from './useAuth'

export function AuthGate({ children }: { children: ReactNode }) {
  const location = useLocation()
  const status = useAuthStatus()
  const needsSetup = status.data?.needsSetup === true
  const me = useMe(status.isSuccess && !needsSetup)

  if (status.isPending) {
    return <CenteredSpinner />
  }

  if (status.isError) {
    return (
      <div className="flex min-h-screen items-center justify-center px-4">
        <div className="w-full max-w-sm rounded-2xl border border-border bg-surface p-6 text-center shadow-xl shadow-black/30">
          <Icon name="cloud_off" className="text-[32px] text-muted" />
          <h1 className="mt-3 text-lg font-semibold text-text">Can't reach the hub</h1>
          <p className="mt-1 text-sm text-muted">The API did not respond. Check that the service is running.</p>
          <button
            type="button"
            onClick={() => void status.refetch()}
            className="mt-5 w-full cursor-pointer rounded-lg bg-accent px-3 py-2 text-sm font-medium text-white transition hover:bg-accent/90"
          >
            Try again
          </button>
        </div>
      </div>
    )
  }

  if (needsSetup) {
    return <Navigate to="/setup" replace />
  }

  if (me.isPending) {
    return <CenteredSpinner />
  }

  if (!me.data) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }

  return <>{children}</>
}

function CenteredSpinner() {
  return (
    <div className="flex min-h-screen items-center justify-center">
      <Spinner />
    </div>
  )
}
