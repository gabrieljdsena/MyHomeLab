import { describe, expect, it } from 'vitest'
import {
  appendHistory,
  loadHistory,
  MAX_HISTORY_ENTRIES,
  resultSummary,
  saveHistory,
  type StorageLike,
  type WorkbenchHistoryEntry,
} from './workbenchHistory'

function entry(sql: string, ok = true, summary = '1 row returned'): WorkbenchHistoryEntry {
  return { sql, at: '2026-10-17T12:00:00.000Z', ok, summary }
}

function memoryStorage(initial?: string): StorageLike & { data: Map<string, string> } {
  const data = new Map<string, string>()
  if (initial !== undefined) {
    data.set('myhomelab.workbench-history', initial)
  }

  return {
    data,
    getItem: (key) => data.get(key) ?? null,
    setItem: (key, value) => {
      data.set(key, value)
    },
  }
}

describe('appendHistory', () => {
  it('prepends the newest entry', () => {
    const result = appendHistory([entry('SELECT 1')], entry('SELECT 2'))
    expect(result.map((item) => item.sql)).toEqual(['SELECT 2', 'SELECT 1'])
  })

  it('replaces a consecutive duplicate statement', () => {
    const result = appendHistory([entry('SELECT 1'), entry('SELECT 2')], entry('SELECT 1'))
    expect(result.map((item) => item.sql)).toEqual(['SELECT 1', 'SELECT 2'])
  })

  it('keeps at most MAX_HISTORY_ENTRIES', () => {
    let list: WorkbenchHistoryEntry[] = []
    for (let i = 0; i < 15; i += 1) {
      list = appendHistory(list, entry(`SELECT ${i}`))
    }

    expect(list).toHaveLength(MAX_HISTORY_ENTRIES)
    expect(list[0].sql).toBe('SELECT 14')
    expect(list.at(-1)?.sql).toBe('SELECT 5')
  })
})

describe('loadHistory and saveHistory', () => {
  it('round-trips entries', () => {
    const storage = memoryStorage()
    saveHistory([entry('SELECT 1', false, 'boom')], storage)
    expect(loadHistory(storage)).toEqual([entry('SELECT 1', false, 'boom')])
  })

  it('returns an empty list for corrupt JSON', () => {
    expect(loadHistory(memoryStorage('{not json'))).toEqual([])
  })

  it('returns an empty list for non-array payloads', () => {
    expect(loadHistory(memoryStorage('{"sql":"SELECT 1"}'))).toEqual([])
  })

  it('drops malformed entries', () => {
    const raw = JSON.stringify([{ sql: 'SELECT 1' }, entry('SELECT 2')])
    expect(loadHistory(memoryStorage(raw))).toEqual([entry('SELECT 2')])
  })
})

describe('resultSummary', () => {
  it('describes returned rows', () => {
    expect(resultSummary({ columns: ['a'], rowCount: 3, truncated: false })).toBe('3 rows returned')
  })

  it('uses the singular for one row', () => {
    expect(resultSummary({ columns: ['a'], rowCount: 1, truncated: false })).toBe('1 row returned')
  })

  it('describes affected rows for DML', () => {
    expect(resultSummary({ columns: [], rowCount: 2, truncated: false })).toBe('2 rows affected')
  })

  it('marks truncated results', () => {
    expect(resultSummary({ columns: ['a'], rowCount: 500, truncated: true })).toBe('500 rows (truncated)')
  })
})
