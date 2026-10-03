import { Icon } from './Icon'
import { PAGE_SIZE_OPTIONS, pageItems } from '../lib/pagination'

export function Pagination({
  page,
  pageSize,
  totalCount,
  totalPages,
  onPageChange,
  onPageSizeChange,
}: {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
}) {
  const start = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const end = Math.min(page * pageSize, totalCount)

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-surface px-4 py-3">
      <p className="text-xs text-muted" aria-live="polite">
        Showing <span className="font-medium text-text">{start}–{end}</span> of{' '}
        <span className="font-medium text-text">{totalCount}</span>
      </p>
      <div className="flex flex-wrap items-center gap-2">
        <label className="flex items-center gap-2 text-xs text-muted">
          Rows
          <select
            value={pageSize}
            onChange={(e) => onPageSizeChange(Number(e.target.value))}
            aria-label="Rows per page"
            className="cursor-pointer rounded-lg border border-border bg-surface-2 px-2 py-1.5 text-xs text-text outline-none transition hover:border-accent/60 focus:border-accent"
          >
            {PAGE_SIZE_OPTIONS.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>
        <div className="flex items-center gap-1" role="navigation" aria-label="Log pages">
          <button
            type="button"
            onClick={() => onPageChange(page - 1)}
            disabled={page <= 1}
            aria-label="Previous page"
            className="cursor-pointer rounded-lg p-2 text-muted transition hover:bg-surface-2 hover:text-text disabled:cursor-default disabled:opacity-40 disabled:hover:bg-transparent disabled:hover:text-muted"
          >
            <Icon name="chevron_left" className="text-[18px]" />
          </button>
          {pageItems(page, totalPages).map((item, i) =>
            item === '…' ? (
              <span key={`gap-${i}`} className="px-1 text-xs text-muted/60" aria-hidden="true">
                …
              </span>
            ) : (
              <button
                key={item}
                type="button"
                onClick={() => onPageChange(item)}
                disabled={item === page}
                aria-label={`Page ${item}`}
                aria-current={item === page ? 'page' : undefined}
                className={`min-w-8 cursor-pointer rounded-lg px-2 py-1.5 text-xs tabular-nums transition ${
                  item === page
                    ? 'bg-accent-soft font-semibold text-accent'
                    : 'text-muted hover:bg-surface-2 hover:text-text'
                } disabled:cursor-default`}
              >
                {item}
              </button>
            ),
          )}
          <button
            type="button"
            onClick={() => onPageChange(page + 1)}
            disabled={page >= totalPages}
            aria-label="Next page"
            className="cursor-pointer rounded-lg p-2 text-muted transition hover:bg-surface-2 hover:text-text disabled:cursor-default disabled:opacity-40 disabled:hover:bg-transparent disabled:hover:text-muted"
          >
            <Icon name="chevron_right" className="text-[18px]" />
          </button>
        </div>
      </div>
    </div>
  )
}
