import { useState } from 'react'
import { useCategories, useCreateApp, useUpdateApp } from '../features/apps/useApps'
import type { AppDetail, CreateAppInput } from '../lib/types'
import { HttpError } from '../api/client'
import { Icon } from './Icon'

const inputClass =
  'w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text placeholder:text-muted/60 outline-none transition focus:border-accent'

const INTERVAL_OPTIONS = [
  { label: '10 seconds', value: 10_000 },
  { label: '30 seconds', value: 30_000 },
  { label: '1 minute', value: 60_000 },
  { label: '5 minutes', value: 300_000 },
]

function Toggle({
  checked,
  onChange,
  label,
}: {
  checked: boolean
  onChange: (value: boolean) => void
  label: string
}) {
  return (
    <button
      type="button"
      onClick={() => onChange(!checked)}
      className="flex w-full cursor-pointer items-center justify-between"
    >
      <span className="text-[13px] text-muted">{label}</span>
      <span
        className={`relative h-5 w-9 rounded-full transition ${checked ? 'bg-accent' : 'bg-surface-2'}`}
        aria-hidden
      >
        <span
          className={`absolute top-0.5 size-4 rounded-full bg-white transition-all ${
            checked ? 'left-4.5' : 'left-0.5'
          }`}
        />
      </span>
    </button>
  )
}

function toForm(app: AppDetail | null): CreateAppInput {
  return {
    name: app?.name ?? '',
    url: app?.url ?? '',
    description: app?.description ?? '',
    icon: app?.icon ?? 'web',
    category: app?.category ?? 'other',
    port: app?.port ?? null,
    tags: app?.tags ?? [],
    healthCheckEnabled: app?.healthCheckEnabled ?? true,
    healthCheckIntervalMs: app?.healthCheckIntervalMs ?? 30_000,
    sortOrder: app?.sortOrder ?? 0,
  }
}

export function AppForm({
  app,
  onClose,
}: {
  app: AppDetail | null
  onClose: () => void
}) {
  const categories = useCategories()
  const createMutation = useCreateApp()
  const updateMutation = useUpdateApp()

  const [form, setForm] = useState<CreateAppInput>(() => toForm(app))
  const [tagsText, setTagsText] = useState(() => (app ? app.tags.join(', ') : ''))
  const [enabled, setEnabled] = useState(() => app?.isEnabled ?? true)
  const [error, setError] = useState<string | null>(null)

  const set = <K extends keyof CreateAppInput>(key: K, value: CreateAppInput[K]) =>
    setForm((f) => ({ ...f, [key]: value }))

  const submit = async () => {
    setError(null)
    const payload: CreateAppInput = {
      ...form,
      tags: tagsText
        .split(',')
        .map((t) => t.trim())
        .filter(Boolean),
    }
    try {
      if (app) {
        await updateMutation.mutateAsync({ id: app.id, input: payload })
      } else {
        await createMutation.mutateAsync(payload)
      }
      onClose()
    } catch (err) {
      if (err instanceof HttpError) {
        setError(err.detail ?? err.title)
      } else {
        setError('Something went wrong. Please try again.')
      }
    }
  }

  const busy = createMutation.isPending || updateMutation.isPending

  return (
    <div className="fixed inset-0 z-40 flex justify-end bg-black/50 backdrop-blur-sm" onClick={onClose}>
      <aside
        className="h-full w-full max-w-md overflow-y-auto border-l border-border bg-surface p-6"
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-label={app ? `Edit ${app.name}` : 'New app'}
      >
        <div className="mb-6 flex items-center justify-between">
          <h2 className="flex items-center gap-2 text-lg font-semibold">
            <Icon name={app ? 'edit_square' : 'add_circle'} className="text-accent" />
            {app ? 'Edit app' : 'Add app'}
          </h2>
          <button type="button" onClick={onClose} className="cursor-pointer text-muted transition hover:text-text">
            <Icon name="close" />
          </button>
        </div>

        <div className="flex flex-col gap-4">
          {error && (
            <p className="rounded-lg border border-down/40 bg-down/10 px-3 py-2 text-sm text-down">
              {error}
            </p>
          )}

          <label className="block">
            <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-muted">
              Name *
            </span>
            <input
              className={inputClass}
              value={form.name}
              onChange={(e) => set('name', e.target.value)}
              placeholder="Jellyfin"
              autoFocus
            />
          </label>

          <label className="block">
            <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-muted">
              URL *
            </span>
            <input
              className={inputClass}
              value={form.url}
              onChange={(e) => set('url', e.target.value)}
              placeholder="http://192.168.15.22:8096"
            />
          </label>

          <label className="block">
            <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-muted">
              Description
            </span>
            <textarea
              className={`${inputClass} min-h-18 resize-y`}
              value={form.description}
              onChange={(e) => set('description', e.target.value)}
              placeholder="What is this service?"
            />
          </label>

          <div className="grid grid-cols-2 gap-3">
            <label className="block">
              <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-muted">
                Icon
              </span>
              <input
                className={inputClass}
                value={form.icon}
                onChange={(e) => set('icon', e.target.value)}
                list="icon-options"
                placeholder="movie"
              />
              <datalist id="icon-options">
                {['movie', 'forum', 'memory', 'dns', 'travel_explore', 'chat', 'web', 'terminal', 'image', 'database'].map(
                  (o) => (
                    <option key={o} value={o} />
                  ),
                )}
              </datalist>
            </label>

            <label className="block">
              <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-muted">
                Category
              </span>
              <input
                className={inputClass}
                value={form.category}
                onChange={(e) => set('category', e.target.value)}
                list="category-options"
                placeholder="media"
              />
              <datalist id="category-options">
                {(categories.data ?? []).map((c) => (
                  <option key={c} value={c} />
                ))}
              </datalist>
            </label>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <label className="block">
              <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-muted">
                Port
              </span>
              <input
                className={inputClass}
                type="number"
                min={1}
                max={65535}
                value={form.port ?? ''}
                onChange={(e) => set('port', e.target.value === '' ? null : Number(e.target.value))}
                placeholder="8096"
              />
            </label>

            <label className="block">
              <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-muted">
                Sort order
              </span>
              <input
                className={inputClass}
                type="number"
                min={0}
                value={form.sortOrder}
                onChange={(e) => set('sortOrder', Number(e.target.value))}
              />
            </label>
          </div>

          <label className="block">
            <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-muted">
              Tags
            </span>
            <input
              className={inputClass}
              value={tagsText}
              onChange={(e) => setTagsText(e.target.value)}
              placeholder="media, video"
            />
          </label>

          <label className="block">
            <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-muted">
              Health check interval
            </span>
            <select
              className={inputClass}
              value={form.healthCheckIntervalMs}
              onChange={(e) => set('healthCheckIntervalMs', Number(e.target.value))}
            >
              {INTERVAL_OPTIONS.map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </select>
          </label>

          <Toggle
            checked={form.healthCheckEnabled}
            onChange={(v) => set('healthCheckEnabled', v)}
            label="Enable background health checks"
          />

          {app && <Toggle checked={enabled} onChange={setEnabled} label="Visible on dashboard" />}

          <div className="mt-2 flex items-center gap-3">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 cursor-pointer rounded-lg border border-border px-4 py-2 text-sm font-medium text-muted transition hover:border-border hover:bg-surface-2 hover:text-text"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={submit}
              disabled={busy || !form.name.trim() || !form.url.trim()}
              className="flex flex-1 cursor-pointer items-center justify-center gap-2 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-50"
            >
              {busy && <span className="size-3.5 animate-spin rounded-full border-2 border-white/40 border-t-white" />}
              {app ? 'Save changes' : 'Add app'}
            </button>
          </div>
        </div>
      </aside>
    </div>
  )
}