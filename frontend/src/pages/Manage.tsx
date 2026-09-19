import { useState } from 'react'
import { useApps, useDeleteApp, usePatchApp, useProbeApp } from '../features/apps/useApps'
import type { AppDetail } from '../lib/types'
import { AppForm } from '../components/AppForm'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { Icon, Spinner } from '../components/Icon'
import { StatusLabel } from '../components/StatusDot'
import { formatLatency, timeAgo } from '../lib/format'

export function Manage() {
  const { data: apps, isLoading, isError, refetch } = useApps()
  const deleteMutation = useDeleteApp()
  const patchMutation = usePatchApp()
  const probeMutation = useProbeApp()

  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<AppDetail | null>(null)
  const [deleting, setDeleting] = useState<AppDetail | null>(null)
  const [probingId, setProbingId] = useState<string | null>(null)

  const openCreate = () => {
    setEditing(null)
    setFormOpen(true)
  }

  const openEdit = (app: AppDetail) => {
    setEditing(app)
    setFormOpen(true)
  }

  const toggleEnabled = (app: AppDetail) =>
    patchMutation.mutate({ id: app.id, changes: { isEnabled: !app.isEnabled } })

  const probe = async (app: AppDetail) => {
    setProbingId(app.id)
    try {
      await probeMutation.mutateAsync(app.id)
    } finally {
      setProbingId(null)
    }
  }

  const confirmDelete = () => {
    if (!deleting) return
    deleteMutation.mutate(deleting.id, {
      onSuccess: () => setDeleting(null),
    })
  }

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    )
  }

  if (isError) {
    return (
      <div className="mx-auto max-w-md rounded-2xl border border-down/40 bg-down/10 p-8 text-center">
        <p className="text-sm text-text">Failed to load apps.</p>
        <button
          type="button"
          onClick={() => refetch()}
          className="mt-4 cursor-pointer rounded-lg border border-border px-4 py-2 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
        >
          Retry
        </button>
      </div>
    )
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Manage apps</h1>
          <p className="mt-1 text-sm text-muted">{apps?.length ?? 0} registered</p>
        </div>
        <button
          type="button"
          onClick={openCreate}
          className="flex cursor-pointer items-center gap-2 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-hover"
        >
          <Icon name="add" filled />
          Add app
        </button>
      </div>

      <div className="overflow-x-auto rounded-2xl border border-border bg-surface">
        <table className="w-full min-w-150 text-left text-sm">
          <thead>
            <tr className="border-b border-border text-xs uppercase tracking-wider text-muted">
              <th className="px-4 py-3 font-medium">App</th>
              <th className="px-4 py-3 font-medium">Category</th>
              <th className="px-4 py-3 font-medium">Port</th>
              <th className="px-4 py-3 font-medium">Health</th>
              <th className="px-4 py-3 font-medium">Checked</th>
              <th className="px-4 py-3 font-medium">Visible</th>
              <th className="px-4 py-3 text-right font-medium">Actions</th>
            </tr>
          </thead>
          <tbody>
            {apps?.map((app) => (
              <tr key={app.id} className="border-b border-border/60 last:border-0 hover:bg-surface-2/50">
                <td className="px-4 py-3">
                  <div className="flex items-center gap-3">
                    <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-accent-soft text-accent">
                      <Icon name={app.icon} className="text-[19px]" />
                    </span>
                    <div className="min-w-0">
                      <p className="truncate font-medium text-text">{app.name}</p>
                      <p className="truncate text-xs text-muted">{app.url}</p>
                    </div>
                  </div>
                </td>
                <td className="px-4 py-3">
                  <span className="rounded-full bg-surface-2 px-2.5 py-0.5 text-xs capitalize text-muted">
                    {app.category}
                  </span>
                </td>
                <td className="px-4 py-3 text-muted">{app.port ?? '—'}</td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    <StatusLabel status={app.healthStatus} />
                    {app.lastLatencyMs !== null && (
                      <span className="text-xs text-muted/70">{formatLatency(app.lastLatencyMs)}</span>
                    )}
                  </div>
                </td>
                <td className="px-4 py-3 text-xs text-muted">{timeAgo(app.lastHealthCheckUtc)}</td>
                <td className="px-4 py-3">
                  <button
                    type="button"
                    onClick={() => toggleEnabled(app)}
                    title={app.isEnabled ? 'Visible' : 'Hidden'}
                    aria-label={app.isEnabled ? 'Disable app' : 'Enable app'}
                    className={`relative h-5 w-9 cursor-pointer rounded-full transition ${
                      app.isEnabled ? 'bg-accent' : 'bg-surface-2'
                    }`}
                  >
                    <span
                      className={`absolute top-0.5 size-4 rounded-full bg-white transition-all ${
                        app.isEnabled ? 'left-4.5' : 'left-0.5'
                      }`}
                    />
                  </button>
                </td>
                <td className="px-4 py-3">
                  <div className="flex items-center justify-end gap-1">
                    <button
                      type="button"
                      onClick={() => probe(app)}
                      disabled={probingId === app.id}
                      className="cursor-pointer rounded-lg p-2 text-muted transition hover:bg-surface-2 hover:text-accent disabled:opacity-50"
                      title="Probe now"
                    >
                      {probingId === app.id ? (
                        <span className="block size-4 animate-spin rounded-full border-2 border-muted/40 border-t-accent" />
                      ) : (
                        <Icon name={app.healthStatus === 'up' ? 'radar' : 'sensors'} className="text-[18px]" />
                      )}
                    </button>
                    <button
                      type="button"
                      onClick={() => openEdit(app)}
                      className="cursor-pointer rounded-lg p-2 text-muted transition hover:bg-surface-2 hover:text-accent"
                      title="Edit"
                    >
                      <Icon name="edit" className="text-[18px]" />
                    </button>
                    <button
                      type="button"
                      onClick={() => setDeleting(app)}
                      className="cursor-pointer rounded-lg p-2 text-muted transition hover:bg-surface-2 hover:text-down"
                      title="Delete"
                    >
                      <Icon name="delete" className="text-[18px]" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {formOpen && (
        <AppForm key={editing?.id ?? 'create'} app={editing} onClose={() => setFormOpen(false)} />
      )}

      <ConfirmDialog
        open={deleting !== null}
        title={`Delete "${deleting?.name}"?`}
        message="This removes the app from the hub registry. This cannot be undone."
        busy={deleteMutation.isPending}
        onConfirm={confirmDelete}
        onCancel={() => setDeleting(null)}
      />
    </div>
  )
}
