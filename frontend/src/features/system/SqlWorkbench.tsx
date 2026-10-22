import { useRef, useState, type KeyboardEvent } from 'react'
import { HttpError } from '../../api/client'
import type { PostgresRelation, SqlWorkbenchResult } from '../../api/system'
import { Icon } from '../../components/Icon'
import { DatabaseTree } from './DatabaseTree'
import { tableQuery } from './schemaHelpers'
import { usePostgresSchema, useRunSql } from './useSystem'
import {
  appendHistory,
  loadHistory,
  resultSummary,
  saveHistory,
  type WorkbenchHistoryEntry,
} from './workbenchHistory'

function formatTime(at: string): string {
  const date = new Date(at)
  if (Number.isNaN(date.getTime())) return ''
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

export function SqlWorkbench() {
  const inputRef = useRef<HTMLTextAreaElement>(null)
  const draftRef = useRef('')
  const [sql, setSql] = useState('')
  const [history, setHistory] = useState<WorkbenchHistoryEntry[]>(() => loadHistory())
  const [historyIndex, setHistoryIndex] = useState(-1)
  const [result, setResult] = useState<SqlWorkbenchResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [target, setTarget] = useState<string | null>(null)
  const schema = usePostgresSchema()
  const effectiveTarget = target ?? schema.data?.current ?? null
  const runSql = useRunSql()

  const record = (statement: string, ok: boolean, summary: string) => {
    setHistory((previous) => {
      const next = appendHistory(previous, {
        sql: statement,
        at: new Date().toISOString(),
        ok,
        summary,
      })
      saveHistory(next)
      return next
    })
  }

  const run = () => {
    const statement = sql.trim()
    if (statement.length === 0 || runSql.isPending) return
    setHistoryIndex(-1)
    setError(null)
    runSql.mutate(
      { sql: statement, database: effectiveTarget },
      {
        onSuccess: (response) => {
          setResult(response)
          record(statement, true, resultSummary(response))
        },
        onError: (failure) => {
          setResult(null)
          const message = failure instanceof HttpError ? (failure.detail ?? failure.message) : 'Request failed.'
          setError(message)
          record(statement, false, message)
        },
      },
    )
  }

  const insertSql = (database: string, statement: string) => {
    setTarget(database)
    setHistoryIndex(-1)
    setSql(statement)
    setError(null)
    setResult(null)
    inputRef.current?.focus()
  }

  const selectRelation = (database: string, relation: PostgresRelation) => {
    insertSql(database, tableQuery(relation.schema, relation.name))
  }

  const changeTarget = (database: string) => {
    setTarget(database)
    setError(null)
    setResult(null)
  }

  const onKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && (!event.shiftKey || event.ctrlKey || event.metaKey)) {
      event.preventDefault()
      run()
      return
    }

    if (
      event.key === 'ArrowUp' &&
      event.currentTarget.selectionStart === 0 &&
      event.currentTarget.selectionEnd === 0
    ) {
      if (history.length === 0 || historyIndex >= history.length - 1) return
      event.preventDefault()
      if (historyIndex === -1) draftRef.current = sql
      const next = historyIndex + 1
      setHistoryIndex(next)
      setSql(history[next].sql)
      return
    }

    if (
      event.key === 'ArrowDown' &&
      historyIndex > -1 &&
      event.currentTarget.selectionStart === event.currentTarget.value.length
    ) {
      event.preventDefault()
      const next = historyIndex - 1
      setHistoryIndex(next)
      setSql(next === -1 ? draftRef.current : history[next].sql)
    }
  }

  const loadEntry = (item: WorkbenchHistoryEntry, index: number) => {
    draftRef.current = sql
    setHistoryIndex(index)
    setSql(item.sql)
    inputRef.current?.focus()
  }

  const clearHistory = () => {
    setHistory([])
    saveHistory([])
  }

  return (
    <section className="mt-4 rounded-2xl border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <Icon name="terminal" className="text-[18px] text-muted" />
        <p className="text-[11px] font-semibold uppercase tracking-wider text-muted">Workbench</p>
        <p className="text-[11px] text-muted/60">DML only — SELECT, INSERT, UPDATE, DELETE</p>
        {schema.data && (
          <label className="ml-auto flex items-center gap-1.5 text-[11px] text-muted/70">
            Run against
            <select
              value={effectiveTarget ?? ''}
              onChange={(event) => changeTarget(event.target.value)}
              className="cursor-pointer rounded-lg border border-border bg-surface-2 px-2 py-1 font-mono text-[11.5px] text-text outline-none transition focus:border-accent/60"
            >
              {schema.data.databases.map((database) => (
                <option key={database.name} value={database.name}>
                  {database.name}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>

      <div className="mt-3 grid items-start gap-3 lg:grid-cols-[13rem_minmax(0,1fr)]">
        <DatabaseTree
          catalog={schema.data}
          isLoading={schema.isLoading}
          isError={schema.isError}
          target={effectiveTarget}
          onSelect={selectRelation}
          onInsertSql={insertSql}
          onSetTarget={changeTarget}
          onRefresh={() => schema.refetch()}
        />

        <div className="min-w-0">
          <div className="flex flex-col gap-2 sm:flex-row">
            <textarea
              ref={inputRef}
              value={sql}
              onChange={(event) => setSql(event.target.value)}
              onKeyDown={onKeyDown}
              rows={3}
              spellCheck={false}
              placeholder="SELECT * FROM apps LIMIT 50;"
              className="min-h-20 flex-1 resize-y rounded-xl border border-border bg-surface-2 px-3 py-2 font-mono text-[12.5px] leading-relaxed text-text outline-none transition placeholder:text-muted/40 focus:border-accent/60"
            />
            <button
              type="button"
              onClick={run}
              disabled={runSql.isPending || sql.trim().length === 0}
              className="flex h-fit shrink-0 cursor-pointer items-center justify-center gap-1.5 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-40"
            >
              <Icon name="play_arrow" filled className="text-[18px]" />
              {runSql.isPending ? 'Running…' : 'Run'}
            </button>
          </div>

          <p className="mt-1.5 text-[11px] text-muted/60">Enter runs · Shift+Enter newline · ↑/↓ history</p>

          {error && (
            <div className="mt-3 flex items-start gap-2 rounded-xl border border-down/40 bg-down/10 px-3 py-2 text-[12.5px] text-down">
              <Icon name="error" className="mt-px text-[16px]" />
              <p className="whitespace-pre-wrap">{error}</p>
            </div>
          )}

          {result && (
            <div className="mt-3">
              <p className="text-[11px] text-muted/70">
                {effectiveTarget && <span className="font-mono text-muted/60">{effectiveTarget} · </span>}
                <span className="font-semibold text-muted">{result.command}</span> ·{' '}
                {resultSummary(result)} · {result.durationMs.toLocaleString()} ms
              </p>
          {result.columns.length > 0 && (
            <div className="mt-2 max-h-96 overflow-auto rounded-xl border border-border">
              <table className="min-w-full border-collapse font-mono text-[12px]">
                <thead className="sticky top-0 bg-surface-2 text-left text-muted">
                  <tr>
                    {result.columns.map((column, index) => (
                      <th key={index} className="whitespace-nowrap px-2.5 py-1.5 font-semibold">
                        {column}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {result.rows.map((row, rowIndex) => (
                    <tr key={rowIndex} className="border-t border-border/60">
                      {row.map((cell, cellIndex) => (
                        <td
                          key={cellIndex}
                          title={cell === null ? 'NULL' : String(cell)}
                          className="max-w-[24rem] truncate whitespace-nowrap px-2.5 py-1.5"
                        >
                          {cell === null ? <span className="italic text-muted/50">NULL</span> : String(cell)}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {history.length > 0 && (
        <div className="mt-4">
          <div className="flex items-center justify-between">
            <p className="text-[11px] font-semibold uppercase tracking-wider text-muted">Recent</p>
            <button
              type="button"
              onClick={clearHistory}
              className="cursor-pointer text-[11px] text-muted/60 transition hover:text-text"
            >
              Clear
            </button>
          </div>
          <ul className="mt-2 space-y-1">
            {history.map((item, index) => (
              <li key={`${item.at}-${index}`}>
                <button
                  type="button"
                  onClick={() => loadEntry(item, index)}
                  title={`${item.sql}\n${item.summary}`}
                  className="flex w-full cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-left transition hover:bg-surface-2"
                >
                  <span className={`size-1.5 shrink-0 rounded-full ${item.ok ? 'bg-up' : 'bg-down'}`} />
                  <span className="min-w-0 flex-1 truncate font-mono text-[11.5px] text-muted">
                    {item.sql}
                  </span>
                  <span className="hidden shrink-0 text-[10.5px] tabular-nums text-muted/50 sm:inline">
                    {item.summary}
                  </span>
                  <span className="shrink-0 text-[10.5px] tabular-nums text-muted/50">{formatTime(item.at)}</span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
        </div>
      </div>
    </section>
  )
}
