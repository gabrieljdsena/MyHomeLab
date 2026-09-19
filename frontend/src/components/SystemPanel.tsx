import { useSystemMetrics } from '../features/system/useSystem'
import { formatBytes, formatUptime } from '../lib/format'
import { Icon, Spinner } from './Icon'
import type { DiskMetric, StorageHealthReading, TemperatureReading } from '../lib/types'

function barColor(percent: number): string {
  if (percent >= 90) return 'bg-gradient-to-r from-down/70 to-down'
  if (percent >= 70) return 'bg-gradient-to-r from-warn/70 to-warn'
  return 'bg-gradient-to-r from-accent/70 to-accent'
}

function lifeColor(percent: number): [text: string, bar: string] {
  if (percent >= 60) return ['text-up', 'bg-up']
  if (percent >= 30) return ['text-warn', 'bg-warn']
  return ['text-down', 'bg-down']
}

function tempColor(celsius: number): string {
  if (celsius >= 75) return 'text-down'
  if (celsius >= 60) return 'text-warn'
  return 'text-up'
}

function UsageRing({ percent, label }: { percent: number; label: string }) {
  const size = 120
  const stroke = 10
  const radius = (size - stroke) / 2
  const circumference = 2 * Math.PI * radius
  const clamped = Math.min(100, Math.max(0, percent))
  const offset = circumference - (clamped / 100) * circumference

  return (
    <div className="relative size-[120px]" aria-label={`${label} ${Math.round(clamped)}%`}>
      <svg width={size} height={size} className="-rotate-90">
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="var(--color-surface-2)"
          strokeWidth={stroke}
        />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="url(#ring-gradient)"
          strokeWidth={stroke}
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          className="transition-[stroke-dashoffset] duration-700 ease-out"
        />
        <defs>
          <linearGradient id="ring-gradient" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="var(--color-accent)" />
            <stop offset="100%" stopColor="var(--color-accent-hover)" />
          </linearGradient>
        </defs>
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="text-2xl font-bold tabular-nums tracking-tight">{Math.round(clamped)}%</span>
        <span className="text-[11px] font-medium uppercase tracking-wider text-muted">{label}</span>
      </div>
    </div>
  )
}

function Bar({ percent, className = '' }: { percent: number; className?: string }) {
  const clamped = Math.min(100, Math.max(0, percent))
  return (
    <div className={`h-1.5 overflow-hidden rounded-full bg-surface-2 ${className}`}>
      <div
        className={`h-full rounded-full transition-[width] duration-700 ease-out ${barColor(percent)}`}
        style={{ width: `${clamped}%` }}
      />
    </div>
  )
}

function Card({
  title,
  icon,
  children,
}: {
  title: string
  icon: string
  children: React.ReactNode
}) {
  return (
    <section className="rounded-2xl border border-border bg-surface p-4">
      <div className="mb-3 flex items-center gap-2">
        <Icon name={icon} className="text-[17px] text-accent" />
        <h3 className="text-sm font-semibold text-text">{title}</h3>
      </div>
      {children}
    </section>
  )
}

function DiskRow({ disk }: { disk: DiskMetric }) {
  return (
    <div>
      <div className="flex items-baseline justify-between gap-2 text-[13px]">
        <span className="truncate font-medium text-text">{disk.name}</span>
        <span className="shrink-0 tabular-nums text-muted">
          {disk.usagePercent > 0 ? `${Math.round(disk.usagePercent)}%` : '—'}
        </span>
      </div>
      <Bar percent={disk.usagePercent} className="mt-1.5" />
      <p className="mt-1 text-[11px] text-muted/70">
        {disk.fileSystem} · {formatBytes(disk.usedBytes)} used of {formatBytes(disk.totalBytes)}
      </p>
    </div>
  )
}

function HealthRow({ health, temp }: { health: StorageHealthReading; temp?: TemperatureReading }) {
  const life = health.remainingLifePercent
  const subParts = [
    temp ? `${Math.round(temp.celsius)}°C` : null,
    health.dataWrittenBytes !== null ? `${formatBytes(health.dataWrittenBytes)} written` : null,
    health.powerOnHours !== null ? `${health.powerOnHours.toLocaleString()} h on` : null,
  ].filter(Boolean) as string[]
  const [lifeText, lifeBar] = life !== null ? lifeColor(life) : ['text-muted', 'bg-up']

  return (
    <div>
      <div className="flex items-baseline justify-between gap-2 text-[13px]">
        <span className="truncate font-medium text-text">{health.model}</span>
        <span className="flex shrink-0 items-center gap-2">
          {temp && <span className={`tabular-nums ${tempColor(temp.celsius)}`}>{Math.round(temp.celsius)}°C</span>}
          {life !== null ? (
            <span className={`text-xs font-semibold tabular-nums ${lifeText}`}>{Math.round(life)}% left</span>
          ) : (
            <span className="text-[11px] text-muted">SMART</span>
          )}
        </span>
      </div>
      {life !== null && (
        <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-surface-2">
          <div
            className={`h-full rounded-full ${lifeBar}`}
            style={{ width: `${Math.max(0, Math.min(100, life))}%` }}
          />
        </div>
      )}
      {subParts.length > 0 && <p className="mt-1 text-[11px] text-muted/70">{subParts.join(' · ')}</p>}
    </div>
  )
}

export function SystemPanel({ className = '' }: { className?: string }) {
  const { data, isLoading, isError, refetch } = useSystemMetrics()

  if (isLoading && !data) {
    return (
      <aside className={`flex flex-col gap-4 ${className}`}>
        <div className="flex items-center gap-3 rounded-2xl border border-border bg-surface p-4">
          <span className="flex size-10 items-center justify-center rounded-xl bg-surface-2">
            <Icon name="monitor_heart" className="text-accent/60" />
          </span>
          <div className="flex-1">
            <p className="h-3.5 w-32 animate-pulse rounded bg-surface-2" />
            <p className="mt-1.5 h-2.5 w-24 animate-pulse rounded bg-surface-2" />
          </div>
        </div>
        <div className="flex min-h-64 items-center justify-center rounded-2xl border border-border bg-surface">
          <Spinner />
        </div>
      </aside>
    )
  }

  if (isError || !data) {
    return (
      <aside className={`flex flex-col gap-4 ${className}`}>
        <section className="rounded-2xl border border-border bg-surface p-6 text-center">
          <Icon name="monitor_heart" className="text-3xl text-muted" />
          <p className="mt-2 text-sm text-muted">System metrics unavailable.</p>
          <button
            type="button"
            onClick={() => refetch()}
            className="mt-4 cursor-pointer rounded-lg border border-border px-4 py-2 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
          >
            Retry
          </button>
        </section>
      </aside>
    )
  }

  const disks = data.disks.filter((d) => d.driveType === 'Fixed' || d.driveType === 'Network')
  const cpuTemp = data.temperatures.find((t) => t.component === 'CPU')
  const diskTemps = data.temperatures.filter((t) => t.component === 'Disk')
  const otherTemps = data.temperatures.filter((t) => t.component !== 'CPU' && t.component !== 'Disk')
  const diskTempByModel = new Map(diskTemps.map((t) => [t.name, t]))

  return (
    <aside className={`flex flex-col gap-4 ${className}`}>
      <section className="flex items-center gap-3 rounded-2xl border border-border bg-surface p-4">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-accent-soft text-accent">
          <Icon name="monitor_heart" filled className="text-[22px]" />
        </span>
        <div className="min-w-0">
          <p className="truncate text-[15px] font-semibold text-text">{data.hostName}</p>
          <p className="inline-flex items-center gap-1 text-[11px] text-muted">
            <span className="size-1.5 rounded-full bg-up" />
            Up {formatUptime(data.uptimeSeconds)}
          </p>
        </div>
      </section>

      <Card title="CPU" icon="speed">
        <div className="flex items-center justify-between gap-4">
          <UsageRing percent={Math.max(0, data.cpuUsagePercent)} label="used" />
          <div className="flex flex-col items-end gap-1 text-right">
            <span className={`text-lg font-semibold tabular-nums ${cpuTemp ? tempColor(cpuTemp.celsius) : 'text-muted'}`}>
              {cpuTemp ? `${Math.round(cpuTemp.celsius)}°C` : '—'}
            </span>
            <span className="text-[11px] text-muted">
              {cpuTemp ? cpuTemp.name : 'temp unavailable'}
            </span>
            <span className="text-[11px] text-muted/70">{data.processorCount} cores</span>
          </div>
        </div>
      </Card>

      <Card title="Memory" icon="memory">
        <div className="flex items-baseline justify-between">
          <p className="text-lg font-semibold tabular-nums tracking-tight">
            {formatBytes(data.usedMemoryBytes)}
          </p>
          <p className="text-xs text-muted">of {formatBytes(data.totalMemoryBytes)}</p>
        </div>
        <Bar percent={data.memoryUsagePercent} className="mt-2" />
        <div className="mt-2 flex justify-between text-[11px] text-muted/70">
          <span>Used {Math.round(data.memoryUsagePercent)}%</span>
          <span>{formatBytes(data.availableMemoryBytes)} available</span>
        </div>
      </Card>

      {otherTemps.length > 0 && (
        <Card title="Temperatures" icon="thermostat">
          <div className="flex flex-col gap-2">
            {otherTemps.map((t) => (
              <div
                key={`${t.component}-${t.name}`}
                className="flex items-center justify-between gap-2 text-[13px]"
              >
                <span className="truncate font-medium text-text">
                  {t.component}
                  {t.name !== t.component ? ` · ${t.name}` : ''}
                </span>
                <span className={`shrink-0 tabular-nums ${tempColor(t.celsius)}`}>
                  {Math.round(t.celsius)}°C
                </span>
              </div>
            ))}
          </div>
        </Card>
      )}

      {(disks.length > 0 || data.storageHealth.length > 0) && (
        <Card title="Storage" icon="storage">
          <div className="flex flex-col gap-3.5">
            {disks.map((disk) => (
              <DiskRow key={disk.name} disk={disk} />
            ))}
            {data.storageHealth.length > 0 && (
              <div className="flex flex-col gap-3 border-t border-border pt-3.5">
                {data.storageHealth.map((health) => (
                  <HealthRow
                    key={health.model}
                    health={health}
                    temp={diskTempByModel.get(health.model)}
                  />
                ))}
              </div>
            )}
            {diskTemps.length > 0 && data.storageHealth.length === 0 && (
              <div className="flex flex-col gap-2 border-t border-border pt-3.5">
                {diskTemps.map((t) => (
                  <div
                    key={`disk-temp-${t.name}`}
                    className="flex items-center justify-between gap-2 text-[13px]"
                  >
                    <span className="truncate font-medium text-text">{t.name}</span>
                    <span className={`shrink-0 tabular-nums ${tempColor(t.celsius)}`}>
                      {Math.round(t.celsius)}°C
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </Card>
      )}

      <section className="rounded-2xl border border-border bg-surface px-4 py-3 text-[11px] leading-loose text-muted">
        <p className="truncate">{data.operatingSystem}</p>
        <p className="flex items-center justify-between gap-2">
          <span>{data.architecture}</span>
          <span className="truncate text-muted/70">{data.runtimeVersion}</span>
        </p>
      </section>
    </aside>
  )
}
