import type { AppDetail } from '../lib/types'
import { formatLatency } from '../lib/format'
import { Icon } from './Icon'
import { StatusDot } from './StatusDot'

export function AppCard({ app }: { app: AppDetail }) {
  const open = () => window.open(app.url, '_blank', 'noopener,noreferrer')

  return (
    <button
      type="button"
      onClick={open}
      disabled={!app.isEnabled}
      className={`group relative flex w-full cursor-pointer flex-col gap-3 rounded-2xl border border-border bg-surface p-4 text-left transition duration-150 ${
        app.isEnabled
          ? 'hover:-translate-y-0.5 hover:border-accent/60 hover:shadow-lg hover:shadow-black/30'
          : 'cursor-not-allowed opacity-55'
      }`}
    >
      <div className="flex items-start justify-between">
        <span className="flex size-11 items-center justify-center rounded-xl bg-accent-soft text-accent transition group-hover:bg-accent/25">
          <Icon name={app.icon} className="text-[26px]" />
        </span>
        <StatusDot status={app.healthStatus} />
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
      <span className="sr-only">Open {app.name}</span>
    </button>
  )
}