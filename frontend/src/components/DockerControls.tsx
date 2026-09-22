import { useState } from 'react'
import { useAppDocker, useAppDockerAction } from '../features/docker/useDocker'
import { Icon } from './Icon'
import { ConfirmDialog } from './ConfirmDialog'

function stateColor(state: string): string {
  if (state === 'running') return 'bg-up text-white'
  if (state === 'exited' || state === 'created') return 'bg-muted text-white'
  if (state === 'restarting') return 'bg-warn text-black'
  return 'bg-surface-2 text-muted'
}

export function DockerControls({ appId, containerName }: { appId: string; containerName: string | null }) {
  const { data: container, isLoading, isError, refetch } = useAppDocker(appId, Boolean(containerName))
  const actionMutation = useAppDockerAction()
  const [pendingAction, setPendingAction] = useState<'start' | 'stop' | 'restart' | null>(null)
  const [confirmStop, setConfirmStop] = useState(false)

  if (!containerName) return null

  const run = async (action: 'start' | 'stop' | 'restart') => {
    setPendingAction(action)
    try {
      await actionMutation.mutateAsync({ appId, action })
    } finally {
      setPendingAction(null)
      setConfirmStop(false)
    }
  }

  const busy = pendingAction !== null || actionMutation.isPending

  return (
    <div className="overflow-hidden rounded-xl border border-border bg-surface-2/50 p-2.5">
      <div className="flex items-center gap-2">
        <Icon name="deployed_code" className="text-[16px] text-muted" />
        <span className="truncate text-xs font-medium text-text">{containerName}</span>
        {isLoading ? (
          <span className="ml-auto text-[11px] text-muted">checking…</span>
        ) : isError || !container ? (
          <span className="ml-auto inline-flex items-center gap-1 rounded-full bg-down/15 px-2 py-0.5 text-[11px] font-medium text-down">
            <Icon name="warning" className="text-[12px]" />
            not found
          </span>
        ) : (
          <span className={`ml-auto inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-medium capitalize ${stateColor(container.state)}`}>
            <span className={`size-1.5 rounded-full ${container.state === 'running' ? 'bg-white animate-pulse' : 'bg-current opacity-60'}`} />
            {container.state}
          </span>
        )}
        {!isLoading && (isError || !container) && (
          <button
            type="button"
            onClick={() => refetch()}
            className="cursor-pointer rounded-md border border-border px-2 py-1 text-[11px] text-muted hover:bg-surface hover:text-text"
          >
            Retry
          </button>
        )}
      </div>

      {container && (
        <p className="mt-1 truncate text-[11px] text-muted/70" title={container.status}>
          {container.status} {container.image ? `· ${container.image}` : ''}
        </p>
      )}

      <div className="mt-2.5 flex items-center gap-1.5">
        <button
          type="button"
          onClick={() => run('start')}
          disabled={busy || container?.state === 'running'}
          title="Start container"
          aria-label="Start container"
          className="inline-flex size-8 shrink-0 cursor-pointer items-center justify-center rounded-lg border border-border bg-surface text-muted transition hover:border-up/40 hover:text-up disabled:cursor-not-allowed disabled:opacity-40"
        >
          <Icon name="play_arrow" className="text-[18px]" />
        </button>
        <button
          type="button"
          onClick={() => setConfirmStop(true)}
          disabled={busy || container?.state !== 'running'}
          title="Stop container"
          aria-label="Stop container"
          className="inline-flex size-8 shrink-0 cursor-pointer items-center justify-center rounded-lg border border-border bg-surface text-muted transition hover:border-down/40 hover:text-down disabled:cursor-not-allowed disabled:opacity-40"
        >
          <Icon name="stop" className="text-[18px]" />
        </button>
        <button
          type="button"
          onClick={() => run('restart')}
          disabled={busy}
          title="Restart container"
          aria-label="Restart container"
          className="inline-flex size-8 shrink-0 cursor-pointer items-center justify-center rounded-lg border border-border bg-surface text-muted transition hover:border-accent/40 hover:text-accent disabled:cursor-not-allowed disabled:opacity-40"
        >
          <Icon name="restart_alt" className="text-[18px]" />
        </button>
      </div>

      {pendingAction && (
        <p className="mt-2 flex items-center gap-1.5 text-[11px] text-muted">
          <span className="size-3 animate-spin rounded-full border-2 border-muted/30 border-t-accent" />
          {pendingAction}ing {containerName}…
        </p>
      )}

      {actionMutation.isError && (
        <p className="mt-2 rounded-md border border-down/30 bg-down/10 px-2 py-1 text-[11px] text-down">
          {(actionMutation.error as Error)?.message ?? 'Action failed.'}
        </p>
      )}

      <ConfirmDialog
        open={confirmStop}
        title={`Stop "${containerName}"?`}
        message="This will stop the container. The app will be unreachable until started again."
        confirmLabel="Stop"
        busy={pendingAction === 'stop'}
        onConfirm={() => run('stop')}
        onCancel={() => setConfirmStop(false)}
      />
    </div>
  )
}
