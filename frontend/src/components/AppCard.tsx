import { useState } from 'react'
import type { AppDetail } from '../lib/types'
import { formatLatency } from '../lib/format'
import { Icon } from './Icon'
import { StatusDot } from './StatusDot'
import { HealthStrip } from './HealthStrip'
import { HealthHistoryPanel } from './HealthHistoryPanel'
import { DockerControls } from './DockerControls'

export function AppCard({ app }: { app: AppDetail }) {
  const [expanded, setExpanded] = useState(false)
  const open = () => {
    if (!app.isEnabled) return
    window.open(app.url, '_blank', 'noopener,noreferrer')
  }

  const handleCardClick = () => {
    if (!app.isEnabled || expanded) return
    open()
  }

  const handleCardKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault()
      handleCardClick()
    }
  }

  return (
    <div
      role={app.isEnabled ? 'button' : undefined}
      tabIndex={app.isEnabled ? 0 : undefined}
      onClick={handleCardClick}
      onKeyDown={handleCardKeyDown}
      className={`group relative flex w-full flex-col gap-3 overflow-hidden rounded-2xl border border-border bg-surface p-4 text-left transition duration-150 ${
        app.isEnabled
          ? 'cursor-pointer hover:-translate-y-0.5 hover:border-accent/60 hover:shadow-lg hover:shadow-black/30'
          : 'opacity-55'
      }`}
    >
      <div className="flex items-start justify-between gap-2">
        <div
          className={`flex size-11 shrink-0 items-center justify-center rounded-xl bg-accent-soft text-accent transition ${app.isEnabled ? 'group-hover:bg-accent/25' : ''}`}
          aria-hidden
        >
          <Icon name={app.icon} className="text-[26px]" />
        </div>
        <div className="flex items-center gap-1.5" onClick={(e) => e.stopPropagation()} onKeyDown={(e) => e.stopPropagation()}>
          <StatusDot status={app.healthStatus} />
          {app.healthCheckEnabled && app.isEnabled && (
            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation()
                setExpanded((v) => !v)
              }}
              className={`flex size-7 cursor-pointer items-center justify-center rounded-lg border transition ${
                expanded
                  ? 'border-accent bg-accent-soft text-accent'
                  : 'border-border bg-surface-2 text-muted hover:border-accent/40 hover:text-text'
              }`}
              aria-label={expanded ? 'Hide health history' : 'Show health history'}
              title={expanded ? 'Hide history' : 'Show history'}
            >
              <Icon name={expanded ? 'expand_less' : 'monitoring'} className="text-[16px]" />
            </button>
          )}
        </div>
      </div>

      <div className="min-h-14">
        <h3 className="truncate font-semibold text-text">{app.name}</h3>
        <p className="mt-0.5 line-clamp-2 text-[13px] leading-snug text-muted">{app.description}</p>
      </div>

      {app.tags.length > 0 && (
        <div className="flex flex-wrap items-center gap-1.5">
          {app.tags.map((tag) => (
            <span
              key={tag}
              className="rounded-md bg-surface-2 px-2 py-0.5 text-[11px] font-medium text-muted"
            >
              {tag}
            </span>
          ))}
        </div>
      )}

      {app.healthCheckEnabled && app.isEnabled && <HealthStrip appId={app.id} />}

      {app.dockerContainer && app.isEnabled && (
        <div onClick={(e) => e.stopPropagation()} onKeyDown={(e) => e.stopPropagation()}>
          <DockerControls appId={app.id} containerName={app.dockerContainer} />
        </div>
      )}

      <div className="mt-auto flex items-center justify-between border-t border-border pt-3 text-xs text-muted">
        <span className="inline-flex items-center gap-1">
          <Icon name="bolt" className="text-[15px]" />
          {app.lastLatencyMs !== null ? formatLatency(app.lastLatencyMs) : '—'}
        </span>
        <span className="inline-flex items-center gap-1 text-accent group-hover:text-accent-hover">
          Open
          <Icon name="open_in_new" className="text-[15px]" />
        </span>
      </div>

      {expanded && app.healthCheckEnabled && app.isEnabled && (
        <div onClick={(e) => e.stopPropagation()} onKeyDown={(e) => e.stopPropagation()}>
          <HealthHistoryPanel appId={app.id} />
        </div>
      )}
    </div>
  )
}
