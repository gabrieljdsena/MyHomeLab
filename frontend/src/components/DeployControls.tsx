import { useState } from 'react'
import { useDeployStatus, useStartDeploy } from '../features/apps/useApps'
import type { AppDetail, DeployState } from '../lib/types'
import { Icon } from './Icon'
import { ConfirmDialog } from './ConfirmDialog'
import { HttpError } from '../api/client'

const ACTIVE_STATES: DeployState[] = ['queued', 'building', 'restarting']

const STATE_LABEL: Record<DeployState, string> = {
  queued: 'queued',
  building: 'building',
  restarting: 'restarting',
  done: 'done',
  failed: 'failed',
}

export function DeployControls({ app }: { app: AppDetail }) {
  const statusQuery = useDeployStatus(app.id)
  const startDeploy = useStartDeploy()
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [showLog, setShowLog] = useState(false)

  if (!app.serviceName || !app.buildScriptPath) return null

  const status = statusQuery.data ?? null
  const active = status !== null && ACTIVE_STATES.includes(status.state)
  const busy = active || startDeploy.isPending
  const errorMessage =
    startDeploy.error instanceof HttpError
      ? startDeploy.error.detail ?? startDeploy.error.title
      : startDeploy.error
        ? ((startDeploy.error as Error).message ?? 'Deploy failed.')
        : null
  const connectionLost = statusQuery.isError && status !== null

  const run = async () => {
    try {
      await startDeploy.mutateAsync(app.id)
      setShowLog(false)
    } catch {
      // Error is surfaced through mutation state below.
    } finally {
      setConfirmOpen(false)
    }
  }

  return (
    <div className="overflow-hidden rounded-xl border border-border bg-surface-2/50">
      <button
        type="button"
        onClick={() => setConfirmOpen(true)}
        disabled={busy}
        title={`Run ${app.buildScriptPath}, then restart the "${app.serviceName}" service`}
        className="flex w-full cursor-pointer items-center justify-center gap-2 px-3 py-2 text-xs font-medium text-muted transition hover:text-accent disabled:cursor-not-allowed disabled:opacity-60"
      >
        {busy ? (
          <>
            <span className="size-3.5 animate-spin rounded-full border-2 border-muted/30 border-t-accent" />
            Redeploying {app.name}…{status ? ` (${STATE_LABEL[status.state]})` : ''}
          </>
        ) : (
          <>
            <Icon name="refresh" className="text-[16px]" />
            Redeploy
          </>
        )}
      </button>

      {busy && (
        <p className="border-t border-border px-3 py-2 text-[11px] text-muted/70">
          Running {app.buildScriptPath}, then restarting {app.serviceName}. This can take a few minutes
          and the app may go offline briefly.
        </p>
      )}

      {errorMessage && !busy && (
        <p className="border-t border-border px-3 py-2 text-[11px] text-down">{errorMessage}</p>
      )}

      {status && (
        <div className="border-t border-border px-3 py-2">
          <button
            type="button"
            onClick={() => setShowLog((v) => !v)}
            disabled={!status.outputTail}
            className="flex w-full cursor-pointer items-start gap-1.5 text-left disabled:cursor-default"
          >
            {active ? (
              <span className="mt-0.5 size-3 shrink-0 animate-spin rounded-full border-2 border-muted/30 border-t-accent" />
            ) : (
              <Icon
                name={status.state === 'done' ? 'check_circle' : 'error'}
                className={`mt-px text-[14px] ${status.state === 'done' ? 'text-up' : 'text-down'}`}
              />
            )}
            <span
              className={`flex-1 text-[11px] leading-snug ${
                active ? 'text-muted' : status.state === 'done' ? 'text-up' : 'text-down'
              }`}
            >
              {status.message}
              {connectionLost && (
                <span className="block text-muted/70">Connection lost while polling — retrying…</span>
              )}
            </span>
            {status.outputTail && (
              <Icon name={showLog ? 'expand_less' : 'expand_more'} className="mt-px text-[14px] text-muted" />
            )}
          </button>

          {showLog && status.outputTail && (
            <pre className="mt-2 max-h-40 overflow-auto rounded-md bg-black/30 p-2 text-[10px] leading-relaxed whitespace-pre-wrap text-muted">
              {status.outputTail}
            </pre>
          )}
        </div>
      )}

      <ConfirmDialog
        open={confirmOpen}
        title={`Redeploy "${app.name}"?`}
        message={`Runs ${app.buildScriptPath}, then restarts the "${app.serviceName}" service. The app will be briefly unavailable.`}
        confirmLabel="Redeploy"
        busy={busy}
        onConfirm={run}
        onCancel={() => setConfirmOpen(false)}
      />
    </div>
  )
}
