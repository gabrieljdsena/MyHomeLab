import { useEffect, useMemo, useRef, useState } from 'react'
import { useLogs } from '../features/logs/useLogs'
import { Icon, Spinner } from '../components/Icon'

function firstLine(text: string): string {
  const line = text.split('\n', 1)[0] ?? ''
  return line.length > 160 ? `${line.slice(0, 160)}…` : line
}

function FilterOption({
  label,
  selected,
  onPick,
}: {
  label: string
  selected: boolean
  onPick: () => void
}) {
  return (
    <button
      type="button"
      role="option"
      aria-selected={selected}
      onClick={onPick}
      className={`flex w-full cursor-pointer items-center gap-2 px-3 py-2 text-left text-sm transition hover:bg-surface-2 hover:text-text ${
        selected ? 'bg-accent-soft font-medium text-accent' : 'text-muted'
      }`}
    >
      <span className="min-w-0 flex-1 truncate">{label}</span>
      {selected && <Icon name="check" className="shrink-0 text-[18px]" />}
    </button>
  )
}

export function Logs() {
  const [search, setSearch] = useState('')
  const [application, setApplication] = useState('')
  const [expanded, setExpanded] = useState<number | null>(null)
  const [debounced, setDebounced] = useState('')
  const [filterOpen, setFilterOpen] = useState(false)
  const filterRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(search), 300)
    return () => window.clearTimeout(timer)
  }, [search])

  useEffect(() => {
    if (!filterOpen) return
    const onDown = (e: MouseEvent) => {
      if (filterRef.current && !filterRef.current.contains(e.target as Node)) {
        setFilterOpen(false)
      }
    }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [filterOpen])

  const pickApplication = (value: string) => {
    setApplication(value)
    setFilterOpen(false)
  }

  const { data: logs, isLoading, isError, refetch } = useLogs({
    search: debounced || undefined,
    application: application || undefined,
  })

  const applications = useMemo(
    () => [...new Set((logs ?? []).map((entry) => entry.application))].sort(),
    [logs],
  )

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    )
  }

  if (isError) {
    return (
      <div className="mx-auto max-w-md rounded-2xl border border-down/40 bg-down/10 p-8 text-center">
        <p className="text-sm text-text">Failed to load logs.</p>
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

  return (
    <div>
      <div className="mb-6 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Logs</h1>
          <p className="mt-1 text-sm text-muted">
            {(logs?.length ?? 0) + (logs?.length === 1 ? ' entry' : ' entries')}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <label className="flex items-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-sm text-muted focus-within:border-accent">
            <Icon name="search" className="text-[18px]" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search application or text…"
              className="w-52 bg-transparent text-text outline-none placeholder:text-muted/60"
            />
          </label>
          <div className="relative" ref={filterRef}>
            <button
              type="button"
              onClick={() => setFilterOpen((v) => !v)}
              aria-haspopup="listbox"
              aria-expanded={filterOpen}
              className="flex cursor-pointer items-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-sm text-muted transition hover:border-accent/60 hover:text-text"
            >
              <Icon name="filter_alt" className="text-[18px]" />
              <span className="max-w-40 truncate">{application || 'All applications'}</span>
              <Icon
                name="expand_more"
                className={`text-[16px] transition ${filterOpen ? 'rotate-180' : ''}`}
              />
            </button>
            {filterOpen && (
              <div
                role="listbox"
                aria-label="Filter by application"
                className="absolute right-0 z-30 mt-2 max-h-64 w-56 overflow-auto rounded-xl border border-border bg-surface py-1 shadow-xl shadow-black/30"
              >
                <FilterOption
                  label="All applications"
                  selected={application === ''}
                  onPick={() => pickApplication('')}
                />
                {applications.map((name) => (
                  <FilterOption
                    key={name}
                    label={name}
                    selected={application === name}
                    onPick={() => pickApplication(name)}
                  />
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {logs && logs.length > 0 ? (
        <div className="overflow-hidden rounded-2xl border border-border bg-surface">
          <ul className="divide-y divide-border/60">
            {logs.map((entry) => {
              const open = expanded === entry.id
              return (
                <li key={entry.id}>
                  <button
                    type="button"
                    onClick={() => setExpanded(open ? null : entry.id)}
                    aria-expanded={open}
                    className="flex w-full cursor-pointer items-center gap-3 px-4 py-3 text-left transition hover:bg-surface-2"
                  >
                    <span className="shrink-0 font-mono text-xs tabular-nums text-muted">
                      #{entry.id}
                    </span>
                    <span className="shrink-0 truncate rounded-md bg-accent-soft px-2 py-0.5 text-xs font-medium text-accent">
                      {entry.application}
                    </span>
                    <span className="min-w-0 flex-1 truncate font-mono text-[13px] text-text/90">
                      {firstLine(entry.log)}
                    </span>
                    <Icon
                      name="expand_more"
                      className={`shrink-0 text-[18px] text-muted transition ${open ? 'rotate-180' : ''}`}
                    />
                  </button>
                  {open && (
                    <pre className="max-h-96 overflow-auto whitespace-pre-wrap break-words border-t border-border/60 bg-background/60 px-4 py-3 font-mono text-[12px] leading-relaxed text-text/90">
                      {entry.log}
                    </pre>
                  )}
                </li>
              )
            })}
          </ul>
        </div>
      ) : (
        <div className="rounded-2xl border border-dashed border-border bg-surface/50 p-12 text-center">
          <Icon name="article" className="text-4xl text-muted/40" />
          <p className="mt-3 text-sm font-medium text-text">No log entries</p>
          <p className="mx-auto mt-1 max-w-sm text-xs leading-relaxed text-muted">
            {search || application
              ? 'Nothing matches the current filters.'
              : 'The logs table is empty. Entries appear here as applications write to it.'}
          </p>
        </div>
      )}
    </div>
  )
}
