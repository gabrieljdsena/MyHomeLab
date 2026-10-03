import { useState } from 'react'
import {
  useDeleteMachine,
  useDiscovered,
  useMachines,
  usePatchMachine,
  useScanNetwork,
  useTopology,
} from '../features/machines/useMachines'
import type { CreateMachineInput, DiscoveredDevice, MachineSummary } from '../lib/types'
import { formatLatency } from '../lib/format'
import { MachineCard } from '../components/MachineCard'
import { MachineForm } from '../components/MachineForm'
import { NetworkMap } from '../components/NetworkMap'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { Icon, Spinner } from '../components/Icon'

function timeAgo(iso: string): string {
  const seconds = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 1000))
  if (seconds < 60) return `${seconds}s ago`
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  return `${Math.floor(hours / 24)}d ago`
}

export function Machines() {
  const { data: machines, isLoading, isError, refetch } = useMachines()
  const deleteMutation = useDeleteMachine()
  const patchMutation = usePatchMachine()
  const topologyQuery = useTopology()
  const discoveredQuery = useDiscovered()
  const scanMutation = useScanNetwork()

  const [view, setView] = useState<'map' | 'grid'>('map')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<MachineSummary | null>(null)
  const [prefill, setPrefill] = useState<CreateMachineInput | null>(null)
  const [deleting, setDeleting] = useState<MachineSummary | null>(null)

  const openCreate = () => {
    setEditing(null)
    setPrefill(null)
    setFormOpen(true)
  }

  const openEdit = (machine: MachineSummary) => {
    setEditing(machine)
    setPrefill(null)
    setFormOpen(true)
  }

  const openAddDiscovered = (device: DiscoveredDevice) => {
    setEditing(null)
    setPrefill({
      name: device.hostname ?? device.ipAddress,
      description: device.macAddress ? `Discovered on LAN (${device.macAddress})` : 'Discovered on LAN',
      hostname: device.ipAddress,
      icon: device.suggestedIcon ?? 'computer',
      sortOrder: machines?.length ?? 0,
    })
    setFormOpen(true)
  }

  const confirmDelete = () => {
    if (!deleting) return
    deleteMutation.mutate(deleting.id, {
      onSuccess: () => {
        if (selectedId === deleting.id) setSelectedId(null)
        setDeleting(null)
      },
    })
  }

  const toggleEnabled = (machine: MachineSummary) =>
    patchMutation.mutate({ id: machine.id, changes: { isEnabled: !machine.isEnabled } })

  const selected = machines?.find((m) => m.id === selectedId) ?? null
  const discovery = discoveredQuery.data
  const unknowns = (discovery?.devices ?? []).filter((d) => d.matchedMachineId === null)
  const scanned = discovery && discovery.scannedAtUtc !== '0001-01-01T00:00:00'

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
        <p className="text-sm text-text">Failed to load machines.</p>
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
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Machines</h1>
          <p className="mt-1 text-sm text-muted">
            {(machines?.length ?? 0) + (machines?.length === 1 ? ' PC on the LAN' : ' PCs on the LAN')}
            {unknowns.length > 0 && ` · ${unknowns.length} discovered`}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <div className="flex rounded-lg border border-border bg-surface p-0.5" role="tablist" aria-label="View">
            {(['map', 'grid'] as const).map((v) => (
              <button
                key={v}
                type="button"
                role="tab"
                aria-selected={view === v}
                onClick={() => setView(v)}
                className={`flex cursor-pointer items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium transition ${
                  view === v ? 'bg-accent-soft text-accent' : 'text-muted hover:text-text'
                }`}
              >
                <Icon name={v === 'map' ? 'hub' : 'grid_view'} className="text-[18px]" />
                {v === 'map' ? 'Map' : 'Grid'}
              </button>
            ))}
          </div>
          <button
            type="button"
            onClick={openCreate}
            className="flex cursor-pointer items-center gap-2 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-hover"
          >
            <Icon name="add" filled />
            Add machine
          </button>
        </div>
      </div>

      {view === 'map' && (
        <div className="mb-4 overflow-hidden rounded-2xl border border-border bg-surface">
          {topologyQuery.isLoading ? (
            <div className="flex h-64 items-center justify-center">
              <Spinner />
            </div>
          ) : topologyQuery.isError || !topologyQuery.data ? (
            <div className="p-8 text-center">
              <p className="text-sm text-text">Failed to load the network map.</p>
              <button
                type="button"
                onClick={() => topologyQuery.refetch()}
                className="mt-3 cursor-pointer rounded-lg border border-border px-4 py-2 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
              >
                Retry
              </button>
            </div>
          ) : (
            <NetworkMap
              topology={topologyQuery.data}
              discovered={discovery?.devices ?? []}
              selectedId={selectedId}
              onSelect={setSelectedId}
            />
          )}
          {selected && (
            <div className="flex flex-wrap items-center gap-2 border-t border-border/60 bg-surface-2/40 px-4 py-2.5 text-sm">
              <Icon name={selected.icon} className="text-[20px] text-accent" />
              <span className="font-medium text-text">{selected.name}</span>
              <span className="font-mono text-xs text-muted">{selected.hostname}</span>
              {selected.ipAddress && selected.ipAddress !== selected.hostname && (
                <span className="font-mono text-xs text-muted">{selected.ipAddress}</span>
              )}
              <span className="text-xs text-muted">
                {selected.lastLatencyMs !== null ? formatLatency(selected.lastLatencyMs) : selected.reachability}
              </span>
              <span className="ml-auto flex items-center gap-1">
                <button
                  type="button"
                  onClick={() => toggleEnabled(selected)}
                  className="cursor-pointer rounded-md p-1.5 text-muted transition hover:bg-surface-2 hover:text-text"
                  title={selected.isEnabled ? 'Stop probing' : 'Resume probing'}
                  aria-label={`${selected.isEnabled ? 'Hide' : 'Show'} ${selected.name}`}
                >
                  <Icon name={selected.isEnabled ? 'visibility' : 'visibility_off'} className="text-[18px]" />
                </button>
                <button
                  type="button"
                  onClick={() => openEdit(selected)}
                  className="cursor-pointer rounded-md p-1.5 text-muted transition hover:bg-surface-2 hover:text-text"
                  title="Edit"
                  aria-label={`Edit ${selected.name}`}
                >
                  <Icon name="edit" className="text-[18px]" />
                </button>
                <button
                  type="button"
                  onClick={() => setDeleting(selected)}
                  className="cursor-pointer rounded-md p-1.5 text-muted transition hover:bg-down/10 hover:text-down"
                  title="Delete"
                  aria-label={`Delete ${selected.name}`}
                >
                  <Icon name="delete" className="text-[18px]" />
                </button>
              </span>
            </div>
          )}
        </div>
      )}

      {view === 'grid' && (
        <>
          {machines && machines.length > 0 ? (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {machines.map((machine) => (
                <MachineCard
                  key={machine.id}
                  machine={machine}
                  onEdit={() => openEdit(machine)}
                  onDelete={() => setDeleting(machine)}
                  onToggle={() => toggleEnabled(machine)}
                />
              ))}
            </div>
          ) : (
            <div className="rounded-2xl border border-dashed border-border bg-surface/50 p-12 text-center">
              <Icon name="devices_other" className="text-4xl text-muted/40" />
              <p className="mt-3 text-sm font-medium text-text">No machines registered</p>
              <p className="mx-auto mt-1 max-w-sm text-xs leading-relaxed text-muted">
                Add a LAN PC by its hostname to get reachability, shutdown and restart controls.
              </p>
              <button
                type="button"
                onClick={openCreate}
                className="mt-4 cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-hover"
              >
                Add machine
              </button>
            </div>
          )}
        </>
      )}

      <div className="mt-6 overflow-hidden rounded-2xl border border-border bg-surface">
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-border/60 px-4 py-3">
          <div>
            <h2 className="flex items-center gap-2 text-sm font-semibold">
              <Icon name="radar" className="text-[18px] text-accent" />
              Discovered on LAN
              {unknowns.length > 0 && (
                <span className="rounded-full bg-accent-soft px-2 py-0.5 text-[11px] font-semibold text-accent">
                  {unknowns.length}
                </span>
              )}
            </h2>
            <p className="mt-0.5 text-xs text-muted">
              {scanned
                ? `Last scan ${timeAgo(discovery.scannedAtUtc)} · ${discovery.subnets.join(', ') || 'no subnets'}`
                : 'Devices seen on the LAN that are not in the registry — no fixed IP or hostname needed.'}
            </p>
          </div>
          <button
            type="button"
            onClick={() => scanMutation.mutate()}
            disabled={scanMutation.isPending}
            className="flex cursor-pointer items-center gap-1.5 rounded-lg border border-border px-3.5 py-2 text-sm text-text transition hover:bg-surface-2 disabled:opacity-50"
          >
            {scanMutation.isPending ? (
              <span className="size-4 animate-spin rounded-full border-2 border-muted/30 border-t-accent" />
            ) : (
              <Icon name="refresh" className="text-[18px]" />
            )}
            {scanMutation.isPending ? 'Scanning…' : 'Scan now'}
          </button>
        </div>

        {discoveredQuery.isLoading ? (
          <div className="flex h-24 items-center justify-center">
            <Spinner />
          </div>
        ) : discoveredQuery.isError ? (
          <div className="p-6 text-center">
            <p className="text-sm text-text">Discovery is unavailable.</p>
            <button
              type="button"
              onClick={() => discoveredQuery.refetch()}
              className="mt-2 cursor-pointer rounded-lg border border-border px-4 py-1.5 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
            >
              Retry
            </button>
          </div>
        ) : unknowns.length === 0 ? (
          <div className="p-6 text-center text-xs leading-relaxed text-muted">
            {scanned ? (
              <>
                <p>No unknown devices on the LAN right now — everything seen is registered.</p>
                <p className="mt-1 text-muted/70">
                  If machines also show as unknown, check that ProtonVPN is disconnected: it blocks all LAN traffic.
                </p>
              </>
            ) : (
              'Nothing discovered yet. Background scans run every few minutes, or press Scan now.'
            )}
          </div>
        ) : (
          <ul className="divide-y divide-border/60">
            {unknowns.map((device) => (
              <li key={device.ipAddress} className="flex items-center gap-3 px-4 py-2.5">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-surface-2">
                  <Icon name={device.suggestedIcon ?? 'device_unknown'} className="text-[20px] text-muted" />
                </span>
                <div className="min-w-0 flex-1">
                  <p className="truncate font-mono text-sm font-medium text-text">
                    {device.hostname ?? device.ipAddress}
                    {device.deviceType && (
                      <span className="ml-2 rounded-md bg-surface-2 px-1.5 py-0.5 font-sans text-[11px] text-muted">
                        {device.deviceType}
                      </span>
                    )}
                  </p>
                  <p className="mt-0.5 flex flex-wrap gap-x-2 text-xs text-muted">
                    {device.hostname && <span className="font-mono">{device.ipAddress}</span>}
                    {device.macAddress && <span className="font-mono">{device.macAddress}</span>}
                    <span>seen {timeAgo(device.lastSeenUtc)}</span>
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => openAddDiscovered(device)}
                  className="flex shrink-0 cursor-pointer items-center gap-1 rounded-lg border border-border px-3 py-1.5 text-xs font-medium text-text transition hover:border-accent/60 hover:text-accent"
                  title={`Register ${device.ipAddress} as a machine`}
                >
                  <Icon name="add" className="text-[16px]" />
                  Add
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      {formOpen && <MachineForm machine={editing} initial={prefill} onClose={() => setFormOpen(false)} />}

      <ConfirmDialog
        open={deleting !== null}
        title={deleting ? `Delete "${deleting.name}"?` : ''}
        message="This only removes the machine from MyHomeLab. The PC itself is untouched."
        confirmLabel="Delete"
        busy={deleteMutation.isPending}
        onConfirm={confirmDelete}
        onCancel={() => setDeleting(null)}
      />

      {patchMutation.isError && (
        <p className="mt-4 rounded-lg border border-down/40 bg-down/10 px-3 py-2 text-sm text-down">
          {(patchMutation.error as Error)?.message ?? 'Update failed.'}
        </p>
      )}
    </div>
  )
}
