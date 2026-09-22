import { useMemo, useRef, useState, useEffect } from 'react'
import { useHealthHistory } from '../features/apps/useHealthHistory'
import { Icon, Spinner } from './Icon'
import { formatLatency } from '../lib/format'

const HOURS_OPTIONS = [
  { label: '1h', value: 1 },
  { label: '6h', value: 6 },
  { label: '24h', value: 24 },
  { label: '7d', value: 168 },
] as const

const LIMIT_BY_HOURS: Record<number, number> = {
  1: 60,
  6: 120,
  24: 200,
  168: 300,
}

function useContainerWidth() {
  const ref = useRef<HTMLDivElement>(null)
  const [width, setWidth] = useState(0)
  useEffect(() => {
    const el = ref.current
    if (!el) return
    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) setWidth(Math.round(entry.contentRect.width))
    })
    observer.observe(el)
    setWidth(Math.round(el.getBoundingClientRect().width))
    return () => observer.disconnect()
  }, [])
  return { ref, width }
}

function niceCeiling(value: number): number {
  if (value <= 0) return 10
  const mag = 10 ** Math.floor(Math.log10(value))
  const norm = value / mag
  const nice = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10
  return nice * mag
}

export function HealthHistoryPanel({ appId }: { appId: string }) {
  const [hours, setHours] = useState<number>(24)
  const limit = LIMIT_BY_HOURS[hours] ?? 200
  const { data, isLoading, isError, refetch } = useHealthHistory(appId, hours, limit, true)
  const { ref, width } = useContainerWidth()

  const points = useMemo(() => data?.points ?? [], [data?.points])
  const uptime = data?.uptime

  const chart = useMemo(() => {
    if (points.length < 2 || width <= 0) return null
    const latencies = points.filter((p) => p.status === 'up' && p.latencyMs !== null).map((p) => p.latencyMs!)
    const maxLatency = latencies.length ? niceCeiling(Math.max(...latencies)) : 100
    const pad = { top: 12, right: 44, bottom: 18, left: 6 }
    const plotW = Math.max(width - pad.left - pad.right, 2)
    const plotH = 96 - pad.top - pad.bottom
    const baselineY = pad.top + plotH
    const toY = (lat: number | null, status: string) => {
      if (status !== 'up' || lat === null) return baselineY
      return pad.top + plotH - (Math.min(lat, maxLatency) / maxLatency) * plotH
    }
    const toX = (i: number) => pad.left + (i / (points.length - 1)) * plotW
    const linePoints = points.map((p, i) => ({ x: toX(i), y: toY(p.latencyMs, p.status), status: p.status }))
    const upSegments: { x1: number; y1: number; x2: number; y2: number }[] = []
    for (let i = 0; i < linePoints.length - 1; i++) {
      const a = linePoints[i]
      const b = linePoints[i + 1]
      if (a.status === 'up' && b.status === 'up') {
        upSegments.push({ x1: a.x, y1: a.y, x2: b.x, y2: b.y })
      }
    }
    return { pad, plotW, plotH, baselineY, maxLatency, linePoints, upSegments }
  }, [points, width])

  return (
    <div className="mt-3 rounded-xl border border-border bg-surface-2/60 p-3">
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-1.5">
          <Icon name="monitoring" className="text-[16px] text-muted" />
          <span className="text-xs font-semibold text-text">Health history</span>
        </div>
        <div className="flex items-center gap-1">
          {HOURS_OPTIONS.map((opt) => (
            <button
              key={opt.value}
              type="button"
              onClick={() => setHours(opt.value)}
              className={`cursor-pointer rounded-full px-2.5 py-1 text-[11px] font-medium transition ${
                hours === opt.value
                  ? 'bg-accent text-white'
                  : 'bg-surface text-muted hover:bg-surface-2 hover:text-text'
              }`}
            >
              {opt.label}
            </button>
          ))}
        </div>
      </div>

      {uptime && (
        <div className="mt-3 grid grid-cols-3 gap-2 text-center">
          <div className="rounded-lg bg-surface px-2 py-2">
            <p className={`text-sm font-bold tabular-nums ${uptime.uptimePercent >= 99 ? 'text-up' : uptime.uptimePercent >= 90 ? 'text-warn' : 'text-down'}`}>
              {uptime.uptimePercent.toFixed(1)}%
            </p>
            <p className="text-[10px] uppercase tracking-wider text-muted">uptime</p>
          </div>
          <div className="rounded-lg bg-surface px-2 py-2">
            <p className="text-sm font-bold tabular-nums text-text">
              {uptime.averageLatencyMs !== null ? formatLatency(Math.round(uptime.averageLatencyMs)) : '—'}
            </p>
            <p className="text-[10px] uppercase tracking-wider text-muted">avg latency</p>
            {uptime.minLatencyMs !== null && uptime.maxLatencyMs !== null && (
              <p className="text-[10px] text-muted/60">
                {uptime.minLatencyMs}–{uptime.maxLatencyMs}ms
              </p>
            )}
          </div>
          <div className="rounded-lg bg-surface px-2 py-2">
            <p className="text-sm font-bold tabular-nums text-text">
              {uptime.totalChecks}
            </p>
            <p className="text-[10px] uppercase tracking-wider text-muted">checks</p>
            <p className="text-[10px] text-muted/60">
              <span className="text-up">{uptime.upCount} up</span> · <span className="text-down">{uptime.downCount} down</span>
            </p>
          </div>
        </div>
      )}

      <div ref={ref} className="mt-3">
        {isLoading && !data ? (
          <div className="flex h-24 items-center justify-center rounded-lg bg-surface/60">
            <Spinner />
          </div>
        ) : isError ? (
          <div className="flex h-24 flex-col items-center justify-center rounded-lg bg-surface/60">
            <p className="text-xs text-muted">Failed to load history.</p>
            <button
              type="button"
              onClick={() => refetch()}
              className="mt-2 cursor-pointer rounded-md border border-border px-3 py-1 text-xs text-muted hover:bg-surface-2 hover:text-text"
            >
              Retry
            </button>
          </div>
        ) : points.length < 2 ? (
          <div className="flex h-24 flex-col items-center justify-center rounded-lg bg-surface/60">
            <Icon name="query_stats" className="text-2xl text-muted/50" />
            <p className="mt-1 text-xs text-muted">Not enough data yet</p>
            <p className="text-[11px] text-muted/60">History builds as probes run every {points.length === 0 ? '30s' : 'interval'}</p>
          </div>
        ) : chart ? (
          <div className="overflow-hidden rounded-lg border border-border bg-surface">
            <svg width={width} height={96} className="block select-none">
              <line x1={chart.pad.left} y1={chart.baselineY} x2={chart.pad.left + chart.plotW} y2={chart.baselineY} stroke="var(--color-border)" strokeWidth="1" />
              <line
                x1={chart.pad.left}
                y1={chart.pad.top}
                x2={chart.pad.left + chart.plotW}
                y2={chart.pad.top}
                stroke="var(--color-border)"
                strokeWidth="1"
                strokeDasharray="3 4"
                opacity="0.6"
              />
              {chart.upSegments.map((seg, i) => (
                <line
                  key={i}
                  x1={seg.x1}
                  y1={seg.y1}
                  x2={seg.x2}
                  y2={seg.y2}
                  stroke="var(--color-accent)"
                  strokeWidth="1.6"
                  strokeLinecap="round"
                />
              ))}
              {chart.linePoints.map((pt, i) =>
                points[i].status === 'down' ? (
                  <circle key={i} cx={pt.x} cy={chart.baselineY} r="2.5" fill="var(--color-down)" />
                ) : points[i].status === 'up' ? (
                  <circle key={i} cx={pt.x} cy={pt.y} r="1.6" fill="var(--color-accent)" opacity="0.9" />
                ) : null,
              )}
              <text x={chart.pad.left + chart.plotW + 4} y={chart.pad.top + 3} fontSize="9" fill="var(--color-muted)">
                {formatLatency(chart.maxLatency)}
              </text>
              <text x={chart.pad.left + chart.plotW + 4} y={chart.baselineY + 3} fontSize="9" fill="var(--color-muted)">
                0
              </text>
            </svg>
            <div className="flex items-center gap-3 border-t border-border px-2 py-1.5 text-[10px] text-muted">
              <span className="inline-flex items-center gap-1.5">
                <span className="size-2 rounded-full bg-accent" /> latency
              </span>
              <span className="inline-flex items-center gap-1.5">
                <span className="size-2 rounded-full bg-down" /> down
              </span>
              <span className="ml-auto tabular-nums">
                {points.length} points · {hours}h window
              </span>
            </div>
          </div>
        ) : null}
      </div>

      {points.length > 0 && (
        <div className="mt-3 flex gap-[2px] overflow-hidden rounded-md">
          {points.slice(-80).map((p, i) => (
            <span
              key={`${p.checkedAtUtc}-${i}`}
              title={`${p.status} ${p.latencyMs !== null ? `${p.latencyMs}ms` : ''} — ${new Date(p.checkedAtUtc).toLocaleString()}`}
              className={`h-1.5 flex-1 rounded-sm ${p.status === 'up' ? 'bg-up' : p.status === 'down' ? 'bg-down' : 'bg-muted/30'}`}
            />
          ))}
        </div>
      )}
    </div>
  )
}
