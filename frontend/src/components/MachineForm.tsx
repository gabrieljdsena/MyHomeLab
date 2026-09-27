import { useState } from 'react'
import { useCreateMachine, useUpdateMachine } from '../features/machines/useMachines'
import type { CreateMachineInput, MachineSummary } from '../lib/types'
import { HttpError } from '../api/client'
import { Icon } from './Icon'

const inputClass =
  'w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text placeholder:text-muted/60 outline-none transition focus:border-accent'

const ICON_OPTIONS = [
  { label: 'Desktop', value: 'computer' },
  { label: 'Laptop', value: 'laptop_mac' },
  { label: 'Windows', value: 'desktop_windows' },
  { label: 'Server', value: 'dns' },
  { label: 'Game console', value: 'sports_esports' },
  { label: 'TV / media', value: 'tv' },
]

function toForm(machine: MachineSummary | null): CreateMachineInput {
  return {
    name: machine?.name ?? '',
    description: machine?.description ?? '',
    hostname: machine?.hostname ?? '',
    icon: machine?.icon ?? 'computer',
    sortOrder: machine?.sortOrder ?? 0,
  }
}

export function MachineForm({
  machine,
  onClose,
}: {
  machine: MachineSummary | null
  onClose: () => void
}) {
  const createMutation = useCreateMachine()
  const updateMutation = useUpdateMachine()

  const [form, setForm] = useState<CreateMachineInput>(() => toForm(machine))
  const [error, setError] = useState<string | null>(null)

  const set = <K extends keyof CreateMachineInput>(key: K, value: CreateMachineInput[K]) =>
    setForm((f) => ({ ...f, [key]: value }))

  const submit = async () => {
    setError(null)
    try {
      if (machine) {
        await updateMutation.mutateAsync({ id: machine.id, input: form })
      } else {
        await createMutation.mutateAsync(form)
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
        aria-label={machine ? `Edit ${machine.name}` : 'New machine'}
      >
        <div className="mb-6 flex items-center justify-between">
          <h2 className="flex items-center gap-2 text-lg font-semibold">
            <Icon name={machine ? 'edit_square' : 'add_circle'} className="text-accent" />
            {machine ? 'Edit machine' : 'Add machine'}
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
              placeholder="Gaming PC"
              autoFocus
            />
          </label>

          <label className="block">
            <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-muted">
              Hostname *
            </span>
            <input
              className={inputClass}
              value={form.hostname}
              onChange={(e) => set('hostname', e.target.value)}
              placeholder="gaming-pc or gaming-pc.lan.local"
            />
            <p className="mt-1 text-[11px] text-muted/60">
              NetBIOS name, FQDN or IPv4. This is the target of the remote <code>shutdown /m</code> call, so
              backslashes, spaces and underscores are rejected.
            </p>
          </label>

          <label className="block">
            <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-muted">
              Description
            </span>
            <input
              className={inputClass}
              value={form.description}
              onChange={(e) => set('description', e.target.value)}
              placeholder="Living room desktop"
            />
          </label>

          <div className="grid grid-cols-2 gap-3">
            <label className="block">
              <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-muted">
                Icon
              </span>
              <select
                className={inputClass}
                value={form.icon}
                onChange={(e) => set('icon', e.target.value)}
              >
                {ICON_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
            </label>

            <label className="block">
              <span className="mb-1 block text-xs font-medium uppercase tracking-wide text-muted">
                Sort order
              </span>
              <input
                type="number"
                min={0}
                className={inputClass}
                value={form.sortOrder}
                onChange={(e) => set('sortOrder', Number(e.target.value))}
              />
            </label>
          </div>

          <div className="rounded-xl border border-border bg-surface-2/50 p-3">
            <p className="flex items-center gap-1.5 text-xs font-medium text-text">
              <Icon name="shield" className="text-[15px] text-muted" />
              Requirements
            </p>
            <ul className="mt-1.5 list-disc space-y-0.5 pl-4 text-[11px] leading-relaxed text-muted/80">
              <li>The hostname must resolve on the hub, and ICMP echo must be allowed to it.</li>
              <li>Offline machines keep the last IP and MAC they were seen with.</li>
            </ul>
          </div>

          <div className="mt-2 flex items-center gap-3">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 cursor-pointer rounded-lg border border-border px-4 py-2 text-sm font-medium text-muted transition hover:bg-surface-2 hover:text-text"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={submit}
              disabled={busy || !form.name.trim() || !form.hostname.trim()}
              className="flex flex-1 cursor-pointer items-center justify-center gap-2 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-50"
            >
              {busy && <span className="size-3.5 animate-spin rounded-full border-2 border-white/40 border-t-white" />}
              {machine ? 'Save changes' : 'Add machine'}
            </button>
          </div>
        </div>
      </aside>
    </div>
  )
}
