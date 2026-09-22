import { useEffect, useMemo, useRef, useState } from 'react'
import type { MouseEvent as ReactMouseEvent } from 'react'
import { useNetworkTraffic } from '../features/system/useNetwork'
import { formatBytesPerSec } from '../lib/format'
import { Icon, Spinner } from './Icon'

const GRAPH_HEIGHT = 120
const PAD = { top: 10, right: 34, bottom: 16, left: 6 }

type Point = { x: number; y: number }

function smoothPath(points: Point[]): string {
  if (points.length === 0) return ''
  if (points.length === 1) return `M ${points[0].x} ${points[0].y}`
  let d = `M ${points[0].x} ${points[0].y}`
  for (let i = 0; i < points.length - 1; i++) {
    const p0 = points[i - 1] ?? points[i]
    const p1 = points[i]
    const p2 = points[i + 1]
    const p3 = points[i + 2] ?? p2
    const cp1x = p1.x + (p2.x - p0.x) / 6
    const cp1y = p1.y + (p2.y - p0.y) / 6
    const cp2x = p2.x - (p3.x - p1.x) / 6
    const cp2y = p2.y - (p3.y - p1.y) / 6
    d += ` C ${cp1x} ${cp1y}, ${cp2x} ${cp2y}, ${p2.x} ${p2.y}`
  }
  return d
}

function niceCeiling(value: number): number {
  if (value <= 0) return 1
  const magnitude = 10 ** Math.floor(Math.log10(value))
  const normalized = value / magnitude
  const nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10
  return nice * magnitude
}

function useContainerWidth() {
  const ref = useRef<HTMLDivElement>(null)
  const [width, setWidth] = useState(0)

  useEffect(() => {
    const el = ref.current
    if (!el) return
    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        setWidth(Math.round(entry.contentRect.width))
      }
    })
    observer.observe(el)
    setWidth(Math.round(el.getBoundingClientRect().width))
    return () => observer.disconnect()
  }, [])

  return { ref, width }
}

function formatClock(utc: string): string {
  return new Date(utc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

export function NetworkGraph({ className = '' }: { className?: string }) {
  const { data, isLoading, isError, refetch } = useNetworkTraffic()
  const { ref, width } = useContainerWidth()
  const [hoverIndex, setHoverIndex] = useState<number | null>(null)

  const samples = useMemo(() => data ?? [], [data])
  const last = samples.at(-1)

  const plotWidth = Math.max(width - PAD.left - PAD.right, 2)
  const plotHeight = GRAPH_HEIGHT - PAD.top - PAD.bottom
  const baselineY = PAD.top + plotHeight

  const chart = useMemo(() => {
    if (samples.length < 2 || plotWidth <= 0) return null
    const maxRate = niceCeiling(
      Math.max(...samples.flatMap((s) => [s.downloadBytesPerSec, s.uploadBytesPerSec]), 1),
    )
    const toPoint = (v: number, i: number): Point => ({
      x: PAD.left + (i / (samples.length - 1)) * plotWidth,
      y: PAD.top + plotHeight - (Math.min(v, maxRate) / maxRate) * plotHeight,
    })
    const down = samples.map((s, i) => toPoint(s.downloadBytesPerSec, i))
    const up = samples.map((s, i) => toPoint(s.uploadBytesPerSec, i))
    const downLine = smoothPath(down)
    const downArea = `${downLine} L ${down[down.length - 1].x} ${baselineY} L ${down[0].x} ${baselineY} Z`
    return { down, up, downLine, downArea, upLine: smoothPath(up), maxRate }
  }, [samples, plotWidth, plotHeight, baselineY])

  const windowSeconds =
    samples.length > 1
      ? Math.round((new Date(samples[samples.length - 1].sampledAtUtc).getTime() -
          new Date(samples[0].sampledAtUtc).getTime()) /
          1000)
      : 0

  const hovered = hoverIndex !== null && samples[hoverIndex] ? samples[hoverIndex] : null
  const hoverChartPoint = chart && hoverIndex !== null ? chart.down[hoverIndex] : null

  const onMouseMove = (e: ReactMouseEvent<SVGSVGElement>) => {
    if (samples.length < 2 || !chart) return
    const rect = e.currentTarget.getBoundingClientRect()
    const x = e.clientX - rect.left
    const index = Math.min(samples.length - 1, Math.max(0, Math.round(((x - PAD.left) / plotWidth) * (samples.length - 1))))
    setHoverIndex(index)
  }

  return (
    <section className={`rounded-2xl border border-border bg-surface p-4 ${className}`}>
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Icon name="lan" className="text-[17px] text-accent" />
          <h3 className="text-sm font-semibold text-text">Network</h3>
        </div>
        {!isLoading && !isError && windowSeconds > 0 && (
          <span className="text-[11px] text-muted/70">{windowSeconds}s window</span>
        )}
      </div>

      <div className="mt-3 flex items-center gap-4">
        <div className="flex items-center gap-1.5">
          <Icon name="south" className="text-[15px] text-accent" />
          <span className="text-lg font-semibold tabular-nums tracking-tight text-text">
            {last ? formatBytesPerSec(last.downloadBytesPerSec) : '—'}
          </span>
        </div>
        <div className="flex items-center gap-1.5">
          <Icon name="north" className="text-[15px] text-upload" />
          <span className="text-lg font-semibold tabular-nums tracking-tight text-text">
            {last ? formatBytesPerSec(last.uploadBytesPerSec) : '—'}
          </span>
        </div>
        <span className="ml-auto flex gap-4 text-[11px] text-muted">
          <span className="inline-flex items-center gap-1.5">
            <span className="size-2 rounded-full bg-accent" />
            Down
          </span>
          <span className="inline-flex items-center gap-1.5">
            <span className="size-2 rounded-full bg-upload" />
            Up
          </span>
        </span>
      </div>

      <div ref={ref} className="relative mt-3">
        {isLoading && !data ? (
          <div className="flex h-24 items-center justify-center rounded-lg bg-surface-2/40">
            <Spinner />
          </div>
        ) : isError || !data ? (
          <div className="flex h-24 flex-col items-center justify-center rounded-lg bg-surface-2/40">
            <p className="text-xs text-muted">Network data unavailable.</p>
            <button
              type="button"
              onClick={() => refetch()}
              className="mt-2 cursor-pointer rounded-md border border-border px-3 py-1 text-xs text-muted transition hover:bg-surface-2 hover:text-text"
            >
              Retry
            </button>
          </div>
        ) : chart ? (
          <div className="relative">
            <svg
              width={width}
              height={GRAPH_HEIGHT}
              onMouseMove={onMouseMove}
              onMouseLeave={() => setHoverIndex(null)}
              className="block cursor-crosshair select-none"
              role="img"
              aria-label="Network throughput over the last few minutes"
            >
              <defs>
                <linearGradient id="net-down-fill" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="var(--color-accent)" stopOpacity="0.28" />
                  <stop offset="100%" stopColor="var(--color-accent)" stopOpacity="0" />
                </linearGradient>
              </defs>

              <line
                x1={PAD.left}
                y1={baselineY}
                x2={PAD.left + plotWidth}
                y2={baselineY}
                stroke="var(--color-border)"
                strokeWidth="1"
              />
              <line
                x1={PAD.left}
                y1={PAD.top}
                x2={PAD.left + plotWidth}
                y2={PAD.top}
                stroke="var(--color-border)"
                strokeWidth="1"
                strokeDasharray="3 4"
                opacity="0.7"
              />

              <path d={chart.downArea} fill="url(#net-down-fill)" />
              <path
                d={chart.downLine}
                fill="none"
                stroke="var(--color-accent)"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <path
                d={chart.upLine}
                fill="none"
                stroke="var(--color-upload)"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
              />

              <text x={PAD.left + plotWidth + 4} y={PAD.top + 3} fontSize="9" fill="var(--color-muted)">
                {formatBytesPerSec(chart.maxRate)}
              </text>
              <text x={PAD.left + plotWidth + 4} y={baselineY + 3} fontSize="9" fill="var(--color-muted)">
                0
              </text>

              {hoverChartPoint && (
                <line
                  x1={hoverChartPoint.x}
                  y1={PAD.top}
                  x2={hoverChartPoint.x}
                  y2={baselineY}
                  stroke="var(--color-muted)"
                  strokeWidth="1"
                  strokeDasharray="2 3"
                  opacity="0.8"
                />
              )}
              {hoverChartPoint && (
                <>
                  <circle cx={hoverChartPoint.x} cy={hoverChartPoint.y} r="3" fill="var(--color-accent)" />
                  <circle
                    cx={hoverChartPoint.x}
                    cy={chart.up[hoverIndex ?? 0].y}
                    r="3"
                    fill="var(--color-upload)"
                  />
                </>
              )}
            </svg>

            {hovered && hoverChartPoint && (
              <div
                className="pointer-events-none absolute top-4 z-10 rounded-xl border border-border bg-background/90 px-2.5 py-2 backdrop-blur"
                style={{ right: 8 }}
              >
                <p className="text-[11px] font-medium text-text">{formatClock(hovered.sampledAtUtc)}</p>
                <p className="mt-1 flex items-center gap-1.5 text-[11px] text-muted">
                  <span className="size-1.5 rounded-full bg-accent" />
                  {formatBytesPerSec(hovered.downloadBytesPerSec)}
                </p>
                <p className="flex items-center gap-1.5 text-[11px] text-muted">
                  <span className="size-1.5 rounded-full bg-upload" />
                  {formatBytesPerSec(hovered.uploadBytesPerSec)}
                </p>
              </div>
            )}
          </div>
        ) : (
          <div className="flex h-24 flex-col items-center justify-center rounded-lg bg-surface-2/40">
            <Icon name="radar" className="text-2xl text-muted/50" />
            <p className="mt-2 text-xs text-muted">Collecting live traffic…</p>
          </div>
        )}
      </div>
    </section>
  )
}