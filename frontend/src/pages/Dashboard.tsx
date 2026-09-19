import { useMemo, useState } from 'react'
import { useApps } from '../features/apps/useApps'
import { AppCard } from '../components/AppCard'
import { SystemPanel } from '../components/SystemPanel'
import { Icon } from '../components/Icon'
import { Spinner } from '../components/Icon'
import { categoryLabel } from '../lib/format'

export function Dashboard() {
  const { data: apps, isLoading, isError, refetch } = useApps()
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState<string | null>(null)

  const categories = useMemo(() => {
    const set = new Set<string>()
    for (const app of apps ?? []) {
      if (app.isEnabled) set.add(app.category)
    }
    return [...set].sort()
  }, [apps])

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase()
    return (apps ?? []).filter((app) => {
      if (category && app.category !== category) return false
      if (!term) return true
      return (
        app.name.toLowerCase().includes(term) ||
        app.description.toLowerCase().includes(term) ||
        app.tags.some((t) => t.toLowerCase().includes(term))
      )
    })
  }, [apps, search, category])

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
        <Icon name="error" className="text-3xl text-down" />
        <p className="mt-3 text-sm text-text">Failed to load apps.</p>
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
    <div className="grid grid-cols-1 items-start gap-6 xl:grid-cols-[minmax(0,1fr)_360px]">
      <div>
        <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-xl font-semibold tracking-tight">Dashboard</h1>
            <p className="mt-1 text-sm text-muted">
              {filtered.length} service{filtered.length === 1 ? '' : 's'} available
            </p>
          </div>

          <div className="relative w-full sm:w-72">
            <Icon name="search" className="absolute left-3 top-1/2 -translate-y-1/2 text-[18px] text-muted" />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search apps and tags…"
              className="w-full rounded-xl border border-border bg-surface py-2.5 pl-10 pr-3 text-sm text-text placeholder:text-muted/60 outline-none transition focus:border-accent"
            />
          </div>
        </div>

        <div className="mb-6 flex flex-wrap gap-2">
          {categories.map((c) => (
            <button
              key={c}
              type="button"
              onClick={() => setCategory(category === c ? null : c)}
              className={`cursor-pointer rounded-full border px-3 py-1 text-xs font-medium transition ${
                category === c
                  ? 'border-accent bg-accent-soft text-accent'
                  : 'border-border bg-surface text-muted hover:border-accent/50 hover:text-text'
              }`}
            >
              {categoryLabel(c)}
            </button>
          ))}
        </div>

        {filtered.length === 0 ? (
          <div className="rounded-2xl border border-dashed border-border p-16 text-center">
            <Icon name="apps" className="text-4xl text-muted/50" />
            <p className="mt-3 text-sm text-muted">
              {search || category ? 'No apps match your filters.' : 'No apps yet. Add one from the Manage page.'}
            </p>
          </div>
        ) : (
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
            {filtered.map((app) => (
              <AppCard key={app.id} app={app} />
            ))}
          </div>
        )}
      </div>

      <SystemPanel className="w-full xl:sticky xl:top-20 xl:self-start" />
    </div>
  )
}