import { useHealthHistory } from '../features/apps/useHealthHistory'
import type { HealthStatus } from '../lib/types'

const statusBg: Record<HealthStatus, string> = {
  up: 'bg-up',
  down: 'bg-down',
  unknown: 'bg-muted/40',
}

function barTitle(point: { status: HealthStatus; latencyMs: number | null; checkedAtUtc: string }): string {
  const time = new Date(point.checkedAtUtc).toLocaleTimeString()
  if (point.status === 'down') return `down — ${time}`
  if (point.status === 'up' && point.latencyMs !== null) return `up ${point.latencyMs}ms — ${time}`
  return `${point.status} — ${time}`
}

export function HealthStrip({ appId, limit = 30 }: { appId: string; limit?: number }) {
  const { data, isLoading } = useHealthHistory(appId, 24, limit, true)

  if (isLoading && !data) {
    return (
      <div className="flex items-center gap-0.5">
        {Array.from({ length: limit }).map((_, i) => (
          <span key={i} className="h-2.5 w-1.5 animate-pulse rounded-full bg-surface-2" />
        ))}
      </div>
    )
  }

  const points = data?.points ?? []
  if (points.length === 0) {
    return <p className="text-[11px] text-muted/50">No health history yet</p>
  }

  const uptime = data?.uptime
  const show = points.slice(-limit)

  return (
    <div className="flex items-center justify-between gap-2 overflow-hidden">
      <div className="flex min-w-0 flex-1 items-center gap-[3px] overflow-hidden" role="img" aria-label={`Health history ${uptime?.uptimePercent ?? 0}% up`}>
        {show.map((p, i) => (
          <span
            key={`${p.checkedAtUtc}-${i}`}
            title={barTitle(p)}
            className={`h-2.5 w-1.5 shrink-0 rounded-full ${statusBg[p.status]} ${p.status === 'up' ? 'opacity-90' : ''}`}
          />
        ))}
      </div>
      {uptime && uptime.totalChecks > 0 && (
        <span
          className="shrink-0 rounded-md bg-surface-2 px-1.5 py-0.5 text-[10px] font-semibold tabular-nums text-muted"
          title={`${uptime.upCount} up / ${uptime.downCount} down — ${uptime.totalChecks} checks in 24h`}
        >
          {uptime.uptimePercent.toFixed(1)}%
        </span>
      )}
    </div>
  )
}
