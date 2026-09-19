import type { HealthStatus } from '../lib/types'

const dotClass: Record<HealthStatus, string> = {
  up: 'bg-up shadow-[0_0_10px_rgba(52,211,153,0.55)]',
  down: 'bg-down shadow-[0_0_10px_rgba(251,113,133,0.55)]',
  unknown: 'bg-neutral-500/70',
}

const labelClass: Record<HealthStatus, string> = {
  up: 'text-up',
  down: 'text-down',
  unknown: 'text-muted',
}

export function StatusDot({
  status,
  className = '',
}: {
  status: HealthStatus
  className?: string
}) {
  return (
    <span
      className={`inline-block size-2 rounded-full ${dotClass[status]} ${className}`}
      title={status}
      aria-label={`status ${status}`}
    />
  )
}

export function StatusLabel({ status }: { status: HealthStatus }) {
  return (
    <span className={`inline-flex items-center gap-1.5 text-xs font-medium ${labelClass[status]}`}>
      <StatusDot status={status} />
      {status}
    </span>
  )
}