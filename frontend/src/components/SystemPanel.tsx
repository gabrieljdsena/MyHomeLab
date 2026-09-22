import type { ReactNode } from 'react'
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

function Ring({ percent, label }: { percent: number; label: string }) {
  const size = 96
  const stroke = 9
  const radius = (size - stroke) / 2
  const circumference = 2 * Math.PI * radius
  const clamped = Math.min(100, Math.max(0, percent))
  const offset = circumference - (clamped / 100) * circumference
  const gradientId = `ring-gradient-${label}`

  return (
    <div className="relative size-24" aria-label={`${label} ${Math.round(clamped)}%`}>
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
          stroke={`url(#${gradientId})`}
          strokeWidth={stroke}
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          className="transition-[stroke-dashoffset] duration-700 ease-out"
        />
        <defs>
          <linearGradient id={gradientId} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="var(--color-accent)" />
            <stop offset="100%" stopColor="var(--color-accent-hover)" />
          </linearGradient>
        </defs>
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="text-xl font-bold tabular-nums tracking-tight">{Math.round(clamped)}%</span>
        <span className="text-[10px] font-medium uppercase tracking-wider text-muted">{label}</span>
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

function Group({ title, icon, children }: { title: string; icon: string; children: ReactNode }) {
  return (
    <div className="border-t border-border px-4 py-4">
      <div className="mb-3 flex items-center gap-2">
        <Icon name={icon} className="text-[15px] text-muted" />
        <h3 className="text-[11px] font-semibold uppercase tracking-wider text-muted">{title}</h3>
      </div>
      {children}
    </div>
  )
}

function Vital({ children }: { children: ReactNode }) {
  return <div className="flex flex-col items-center gap-2 px-4 py-4 text-center">{children}</div>
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
      <aside className={`rounded-2xl border border-border bg-surface ${className}`}>
        <div className="flex items-center gap-3 p-4">
          <span className="size-10 animate-pulse rounded-xl bg-surface-2" />
          <div className="flex-1">
            <p className="h-3.5 w-32 animate-pulse rounded bg-surface-2" />
            <p className="mt-1.5 h-2.5 w-24 animate-pulse rounded bg-surface-2" />
          </div>
        </div>
        <div className="flex min-h-40 items-center justify-center border-t border-border">
          <Spinner />
        </div>
      </aside>
    )
  }

  if (isError || !data) {
    return (
      <aside className={`rounded-2xl border border-border bg-surface p-6 text-center ${className}`}>
        <Icon name="monitor_heart" className="text-3xl text-muted" />
        <p className="mt-2 text-sm text-muted">System metrics unavailable.</p>
        <button
          type="button"
          onClick={() => refetch()}
          className="mt-4 cursor-pointer rounded-lg border border-border px-4 py-2 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
        >
          Retry
        </button>
      </aside>
    )
  }

  const disks = data.disks.filter((d) => d.driveType === 'Fixed' || d.driveType === 'Network')
  const cpuTemp = data.temperatures.find((t) => t.component === 'CPU')
  const diskTemps = data.temperatures.filter((t) => t.component === 'Disk')
  const otherTemps = data.temperatures.filter((t) => t.component !== 'CPU' && t.component !== 'Disk')
  const diskTempByModel = new Map(diskTemps.map((t) => [t.name, t]))

  return (
    <aside className={`overflow-hidden rounded-2xl border border-border bg-surface ${className}`}>
      <div className="flex items-center gap-3 p-4">
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
      </div>

      <div className="flex items-center justify-between gap-3 border-t border-border px-4 py-2.5 text-[11px] text-muted/80">
        <span className="truncate">{data.operatingSystem}</span>
        <span className="shrink-0">
          {data.architecture} · {data.runtimeVersion}
        </span>
      </div>

      <div className="grid grid-cols-2 divide-x divide-border border-t border-border">
        <Vital>
          <Ring percent={data.cpuUsagePercent} label="cpu" />
          <div className="space-y-0.5">
            <p className={`text-sm font-semibold tabular-nums ${cpuTemp ? tempColor(cpuTemp.celsius) : 'text-muted'}`}>
              {cpuTemp ? `${Math.round(cpuTemp.celsius)}°C` : '—'}
            </p>
            <p className="text-[11px] text-muted/70">{cpuTemp ? cpuTemp.name : 'temp unavailable'}</p>
            <p className="text-[11px] text-muted/70">{data.processorCount} cores</p>
          </div>
        </Vital>
        <Vital>
          <Ring percent={data.memoryUsagePercent} label="ram" />
          <div className="space-y-0.5">
            <p className="text-sm font-semibold tabular-nums">{formatBytes(data.usedMemoryBytes)}</p>
            <p className="text-[11px] text-muted/70">of {formatBytes(data.totalMemoryBytes)}</p>
            <p className="text-[11px] text-muted/70">{formatBytes(data.availableMemoryBytes)} free</p>
          </div>
        </Vital>
      </div>

      {otherTemps.length > 0 && (
        <Group title="Temperatures" icon="thermostat">
          <div className="flex flex-col gap-2">
            {otherTemps.map((t) => (
              <div key={`${t.component}-${t.name}`} className="flex items-center justify-between gap-2 text-[13px]">
                <span className="truncate font-medium text-text">
                  {t.component}
                  {t.name !== t.component ? ` · ${t.name}` : ''}
                </span>
                <span className={`shrink-0 tabular-nums ${tempColor(t.celsius)}`}>{Math.round(t.celsius)}°C</span>
              </div>
            ))}
          </div>
        </Group>
      )}

      {(disks.length > 0 || data.storageHealth.length > 0) && (
        <Group title="Storage" icon="storage">
          <div className="flex flex-col gap-3.5">
            {disks.map((disk) => (
              <DiskRow key={disk.name} disk={disk} />
            ))}
            {data.storageHealth.length > 0 && (
              <div className="flex flex-col gap-3 border-t border-border pt-3.5">
                {data.storageHealth.map((health) => (
                  <HealthRow key={health.model} health={health} temp={diskTempByModel.get(health.model)} />
                ))}
              </div>
            )}
            {diskTemps.length > 0 && data.storageHealth.length === 0 && (
              <div className="flex flex-col gap-2 border-t border-border pt-3.5">
                {diskTemps.map((t) => (
                  <div key={`disk-temp-${t.name}`} className="flex items-center justify-between gap-2 text-[13px]">
                    <span className="truncate font-medium text-text">{t.name}</span>
                    <span className={`shrink-0 tabular-nums ${tempColor(t.celsius)}`}>{Math.round(t.celsius)}°C</span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </Group>
      )}
    </aside>
  )
}
