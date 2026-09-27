import { usePostgresMetrics } from '../features/system/useSystem'
import { formatBytes, formatUptime } from '../lib/format'
import { Icon, Spinner } from '../components/Icon'
import { Bar, Ring } from '../components/Metric'

function Tile({
  label,
  value,
  sub,
  children,
}: {
  label: string
  value: string
  sub?: string
  children?: React.ReactNode
}) {
  return (
    <div className="rounded-2xl border border-border bg-surface p-4">
      <p className="text-[11px] font-semibold uppercase tracking-wider text-muted">{label}</p>
      <p className="mt-1.5 text-2xl font-semibold tracking-tight tabular-nums">{value}</p>
      {sub && <p className="mt-0.5 text-[11px] text-muted/70">{sub}</p>}
      {children}
    </div>
  )
}

function Flag({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-2 rounded-xl border border-warn/30 bg-warn/10 px-3 py-2 text-[13px]">
      <span className="truncate text-muted">{label}</span>
      <span className="shrink-0 font-medium tabular-nums text-warn">{value}</span>
    </div>
  )
}

export function Postgres() {
  const { data, isLoading, isError, refetch } = usePostgresMetrics()

  if (isLoading && !data) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    )
  }

  if (isError || !data) {
    return (
      <div className="mx-auto max-w-md rounded-2xl border border-down/40 bg-down/10 p-8 text-center">
        <Icon name="database" className="text-3xl text-muted" />
        <p className="mt-2 text-sm text-text">Failed to read PostgreSQL metrics.</p>
        <p className="mt-1 text-[12px] text-muted">
          The hub needs PostgreSQL 17+ for the checkpoint counter; older servers fail this query.
        </p>
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

  const connPercent = data.connectionsMax > 0 ? (data.connectionsUsed / data.connectionsMax) * 100 : 0
  const cachePercent = data.cacheHitRatio * 100
  const tps = data.transactionsPerSecond
  const tpsText = tps < 10 ? tps.toFixed(2) : Math.round(tps).toLocaleString()

  return (
    <div className="mx-auto max-w-7xl">
      <div className="mb-6 flex items-center gap-3">
        <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-accent-soft text-accent">
          <Icon name="database" filled className="text-[24px]" />
        </span>
        <div className="min-w-0">
          <h1 className="text-xl font-semibold tracking-tight">PostgreSQL</h1>
          <p className="mt-0.5 truncate text-sm text-muted">
            {data.databaseName} · v{data.version} · up {formatUptime(data.uptimeSeconds)} ·{' '}
            {formatBytes(data.sizeBytes)}
          </p>
        </div>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <div className="flex items-center gap-4 rounded-2xl border border-border bg-surface p-4 sm:col-span-2">
          <Ring percent={connPercent} label="conns" />
          <div className="min-w-0 flex-1">
            <p className="text-[11px] font-semibold uppercase tracking-wider text-muted">Connections</p>
            <p className="mt-1 text-lg font-semibold tabular-nums">
              {data.connectionsUsed}
              <span className="text-sm font-normal text-muted"> / {data.connectionsMax}</span>
            </p>
            <p className="mt-0.5 text-[11px] text-muted/70">
              {data.connectionsActive} active · {data.connectionsIdle} idle
            </p>
            <Bar percent={connPercent} className="mt-2" />
          </div>
        </div>

        <div className="flex items-center gap-4 rounded-2xl border border-border bg-surface p-4 sm:col-span-2">
          <Ring percent={cachePercent} label="cache" />
          <div className="min-w-0 flex-1">
            <p className="text-[11px] font-semibold uppercase tracking-wider text-muted">Cache hit</p>
            <p className="mt-1 text-lg font-semibold tabular-nums">{cachePercent.toFixed(2)}%</p>
            <p className="mt-0.5 text-[11px] text-muted/70">shared buffers, since statistics reset</p>
            <Bar percent={cachePercent} className="mt-2" />
          </div>
        </div>

        <Tile label="Throughput" value={`${tpsText} tps`} sub="commits + rollbacks per second" />
        <Tile
          label="WAL"
          value={data.walBytesPerSecond > 0 ? `${formatBytes(data.walBytesPerSecond)}/s` : '0 B/s'}
          sub="write-ahead log generation"
        />
        <Tile
          label="Temp spills"
          value={data.tempBytesPerSecond > 0 ? `${formatBytes(data.tempBytesPerSecond)}/s` : '0 B/s'}
          sub="queries spilling to disk"
        />
        <Tile label="Checkpoints" value={data.checkpointsTotal.toLocaleString()} sub="since statistics reset" />

        <Tile
          label="Transactions"
          value={data.transactionsTotal.toLocaleString()}
          sub={`${data.rollbacksTotal.toLocaleString()} rolled back`}
        />
        <Tile
          label="Deadlocks"
          value={data.deadlocks.toLocaleString()}
          sub={data.deadlocks > 0 ? 'needs attention' : 'none detected'}
        />
        <Tile
          label="Longest query"
          value={
            data.longestQuerySeconds >= 1 ? `${data.longestQuerySeconds.toFixed(1)}s` : '<1s'
          }
          sub="oldest active query on this database"
        />
        <Tile
          label="Waiting on locks"
          value={data.lockWaiters.toLocaleString()}
          sub={data.lockWaiters > 0 ? 'blocked sessions' : 'no blocked sessions'}
        />
      </div>

      {(data.lockWaiters > 0 || data.longestQuerySeconds >= 30 || data.deadlocks > 0) && (
        <div className="mt-4 space-y-2">
          <p className="text-[11px] font-semibold uppercase tracking-wider text-muted">Needs attention</p>
          {data.lockWaiters > 0 && <Flag label="Sessions waiting on a lock" value={String(data.lockWaiters)} />}
          {data.longestQuerySeconds >= 30 && (
            <Flag label="Longest running query" value={`${data.longestQuerySeconds.toFixed(1)}s`} />
          )}
          {data.deadlocks > 0 && <Flag label="Deadlocks" value={String(data.deadlocks)} />}
        </div>
      )}

      <p className="mt-4 text-[11px] leading-relaxed text-muted/60">
        Sampled every 5s from <code className="text-muted/80">/api/system/postgres</code>. The{' '}
        <code className="text-muted/80">pg_stat_*</code> views are cumulative counters, so every rate
        above is a difference between two consecutive samples and reads 0 for the first few seconds
        after the hub starts.
      </p>
    </div>
  )
}
