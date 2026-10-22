export interface WorkbenchHistoryEntry {
  sql: string
  at: string
  ok: boolean
  summary: string
}

export interface StorageLike {
  getItem(key: string): string | null
  setItem(key: string, value: string): void
}

const STORAGE_KEY = 'myhomelab.workbench-history'
export const MAX_HISTORY_ENTRIES = 10

export function appendHistory(
  list: WorkbenchHistoryEntry[],
  entry: WorkbenchHistoryEntry,
): WorkbenchHistoryEntry[] {
  const rest = list.length > 0 && list[0].sql === entry.sql ? list.slice(1) : list
  return [entry, ...rest].slice(0, MAX_HISTORY_ENTRIES)
}

export function loadHistory(storage: StorageLike = localStorage): WorkbenchHistoryEntry[] {
  try {
    const raw = storage.getItem(STORAGE_KEY)
    if (!raw) return []
    const parsed: unknown = JSON.parse(raw)
    if (!Array.isArray(parsed)) return []
    return parsed.filter(isEntry).slice(0, MAX_HISTORY_ENTRIES)
  } catch {
    return []
  }
}

export function saveHistory(list: WorkbenchHistoryEntry[], storage: StorageLike = localStorage): void {
  try {
    storage.setItem(STORAGE_KEY, JSON.stringify(list))
  } catch {
    // history is best-effort; private mode or a full quota must not break the workbench
  }
}

export function resultSummary(result: {
  columns: string[]
  rowCount: number
  truncated: boolean
}): string {
  const rows = `${result.rowCount} row${result.rowCount === 1 ? '' : 's'}`
  if (result.columns.length === 0) return `${rows} affected`
  return result.truncated ? `${rows} (truncated)` : `${rows} returned`
}

function isEntry(value: unknown): value is WorkbenchHistoryEntry {
  if (typeof value !== 'object' || value === null) return false
  const entry = value as Record<string, unknown>
  return (
    typeof entry.sql === 'string' &&
    typeof entry.at === 'string' &&
    typeof entry.ok === 'boolean' &&
    typeof entry.summary === 'string'
  )
}
