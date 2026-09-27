import type { MachineSummary } from '../lib/types'
import { formatLatency } from '../lib/format'
import { Icon } from './Icon'
import { StatusDot } from './StatusDot'

const status: Record<MachineSummary['reachability'], 'up' | 'down' | 'unknown'> = {
  online: 'up',
  offline: 'down',
  unknown: 'unknown',
}

export function MachineCard({
  machine,
  onEdit,
  onDelete,
  onToggle,
}: {
  machine: MachineSummary
  onEdit: () => void
  onDelete: () => void
  onToggle: () => void
}) {
  const offline = machine.reachability === 'offline'
  // When the hostname is itself an IP literal the two chips would render the same text.
  const showIp = machine.ipAddress !== null && machine.ipAddress !== machine.hostname

  return (
    <div
      className={`flex w-full flex-col gap-3 overflow-hidden rounded-2xl border border-border bg-surface p-4 text-left transition duration-150 ${
        machine.isEnabled ? 'hover:border-accent/50' : 'opacity-55'
      }`}
    >
      <div className="flex items-start justify-between gap-2">
        <div
          className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-accent-soft text-accent"
          aria-hidden
        >
          <Icon name={machine.icon} className="text-[26px]" />
        </div>
        <StatusDot status={status[machine.reachability]} />
      </div>

      <div className="min-h-14">
        <h3 className="truncate font-semibold text-text">{machine.name}</h3>
        <p className="mt-0.5 line-clamp-2 text-[13px] leading-snug text-muted">{machine.description}</p>
      </div>

      <div className="flex flex-wrap items-center gap-1.5">
        <span className="inline-flex items-center gap-1 rounded-md bg-surface-2 px-2 py-0.5 text-[11px] font-medium text-muted">
          <Icon name="lan" className="text-[13px]" />
          {machine.hostname}
        </span>
        {showIp && (
          <span className="inline-flex items-center gap-1 rounded-md bg-surface-2 px-2 py-0.5 text-[11px] font-medium text-muted">
            <Icon name="dns" className="text-[13px]" />
            {machine.ipAddress}
          </span>
        )}
        {machine.macAddress && (
          <span className="inline-flex items-center gap-1 rounded-md bg-surface-2 px-2 py-0.5 text-[11px] font-medium text-muted">
            <Icon name="fingerprint" className="text-[13px]" />
            {machine.macAddress}
          </span>
        )}
        {!machine.isEnabled && (
          <span className="rounded-md bg-surface-2 px-2 py-0.5 text-[11px] font-medium text-muted">hidden</span>
        )}
      </div>

      <div className="mt-auto flex items-center justify-between border-t border-border pt-3 text-xs text-muted">
        <span className="inline-flex items-center gap-1">
          <Icon name="bolt" className="text-[15px]" />
          {machine.lastLatencyMs !== null ? formatLatency(machine.lastLatencyMs) : '—'}
        </span>
        <div className="flex items-center gap-1">
          {offline && <span className="text-down">offline</span>}
          <button
            type="button"
            onClick={onToggle}
            className="cursor-pointer rounded-md p-1 text-muted transition hover:bg-surface-2 hover:text-text"
            aria-label={`${machine.isEnabled ? 'Hide' : 'Show'} ${machine.name}`}
            title={machine.isEnabled ? 'Stop probing this machine' : 'Resume probing this machine'}
          >
            <Icon name={machine.isEnabled ? 'visibility' : 'visibility_off'} className="text-[15px]" />
          </button>
          <button
            type="button"
            onClick={onEdit}
            className="cursor-pointer rounded-md p-1 text-muted transition hover:bg-surface-2 hover:text-text"
            aria-label={`Edit ${machine.name}`}
            title="Edit"
          >
            <Icon name="edit" className="text-[15px]" />
          </button>
          <button
            type="button"
            onClick={onDelete}
            className="cursor-pointer rounded-md p-1 text-muted transition hover:bg-down/10 hover:text-down"
            aria-label={`Delete ${machine.name}`}
            title="Delete"
          >
            <Icon name="delete" className="text-[15px]" />
          </button>
        </div>
      </div>
    </div>
  )
}
