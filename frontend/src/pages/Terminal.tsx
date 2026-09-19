import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTerminalConfig, useTerminalExec } from '../features/terminal/useTerminal'
import { Icon, Spinner } from '../components/Icon'

type HistoryEntry = {
  id: number
  command: string
  shell: string
  cwd: string
  resolvedCwd: string
  output: string
  exitCode: number
  durationMs: number
  timedOut: boolean
  executedAtUtc: string
}

const LOCAL_COMMANDS = new Set(['clear', 'cls'])

function formatExit(exitCode: number, timedOut: boolean): string {
  if (timedOut) return 'timed out'
  if (exitCode === 0) return 'ok'
  return `exit ${exitCode}`
}

export function Terminal() {
  const { data: config, isLoading: configLoading, isError: configError } = useTerminalConfig()
  const execMutation = useTerminalExec()

  const [cwdOverride, setCwdOverride] = useState<string | null>(null)
  const [shellOverride, setShellOverride] = useState<string | null>(null)
  const cwd = cwdOverride ?? config?.defaultWorkingDirectory ?? ''
  const shell = shellOverride ?? config?.defaultShell ?? 'powershell'
  const [input, setInput] = useState('')
  const [history, setHistory] = useState<HistoryEntry[]>([])
  const [commandHistory, setCommandHistory] = useState<string[]>([])
  const [historyIndex, setHistoryIndex] = useState<number>(-1)
  const [draft, setDraft] = useState('')

  const inputRef = useRef<HTMLInputElement>(null)
  const scrollRef = useRef<HTMLDivElement>(null)
  const nextId = useRef(0)

  const allowedShells = useMemo(() => {
    const raw = config?.allowedShells ?? ['powershell', 'cmd']
    return [...new Set(raw.map((s) => s.toLowerCase()))]
  }, [config])

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight
    }
  }, [history, execMutation.isPending])

  // Keep focus on input after every command and after pending finishes.
  // "Running…" was blocking focus because disabled inputs cannot be focused (MDN)
  // and the Run button was stealing focus on click. We keep the input readOnly
  // (still focusable) and refocus via callback ref + effects — pattern from
  // https://stackoverflow.com/questions/22573494/input-losing-focus-when-rerendering
  // and https://developer.mozilla.org/en-US/docs/Web/HTML/Reference/Attributes/readonly
  useEffect(() => {
    if (!execMutation.isPending) {
      const id = setTimeout(() => {
        const el = inputRef.current
        if (el) {
          el.focus()
          // move caret to end (common terminal UX)
          const len = el.value.length
          try {
            el.setSelectionRange(len, len)
          } catch {
            // ignore for non-text inputs
          }
        }
      }, 0)
      return () => clearTimeout(id)
    }
  }, [execMutation.isPending, history.length])

  const focusInput = useCallback(() => {
    setTimeout(() => {
      const el = inputRef.current
      if (el) {
        el.focus()
        const len = el.value.length
        try {
          el.setSelectionRange(len, len)
        } catch {
          // ignore
        }
      }
    }, 0)
  }, [])

  const handleClear = useCallback(() => {
    setHistory([])
    setTimeout(() => inputRef.current?.focus(), 0)
  }, [])

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault()
      if (execMutation.isPending) return
      const raw = input.trimEnd()
      const trimmed = raw.trim()
      if (!trimmed) return

      // local commands
      if (LOCAL_COMMANDS.has(trimmed.toLowerCase())) {
        setHistory((prev) => [
          ...prev,
          {
            id: nextId.current++,
            command: trimmed,
            shell,
            cwd,
            resolvedCwd: cwd,
            output: '',
            exitCode: 0,
            durationMs: 0,
            timedOut: false,
            executedAtUtc: new Date().toISOString(),
          },
        ])
        setCommandHistory((prev) => [...prev, trimmed])
        setInput('')
        setHistoryIndex(-1)
        handleClear()
        setTimeout(() => inputRef.current?.focus(), 0)
        return
      }

      if (trimmed.toLowerCase() === 'help') {
        setHistory((prev) => [
          ...prev,
          {
            id: nextId.current++,
            command: trimmed,
            shell,
            cwd,
            resolvedCwd: cwd,
            output:
              'Available commands:\n  clear / cls  — clear the screen\n  help         — show this help\n  cd <path>    — change directory (persists across commands)\n\nAny other input is executed on the host via the selected shell.\nShells are sandboxed per request with a timeout of ' +
              (config?.timeoutSeconds ?? 30) +
              's.',
            exitCode: 0,
            durationMs: 0,
            timedOut: false,
            executedAtUtc: new Date().toISOString(),
          },
        ])
        setCommandHistory((prev) => [...prev, trimmed])
        setInput('')
        setHistoryIndex(-1)
        setTimeout(() => inputRef.current?.focus(), 0)
        return
      }

      const requestCwd = cwd
      const requestShell = shell

      setInput('')
      setHistoryIndex(-1)
      setCommandHistory((prev) => [...prev, trimmed])
      setDraft('')

      try {
        const result = await execMutation.mutateAsync({
          command: trimmed,
          cwd: requestCwd,
          shell: requestShell,
        })

        setCwdOverride(result.resolvedCwd)

        setHistory((prev) => [
          ...prev,
          {
            id: nextId.current++,
            command: result.command,
            shell: result.shell,
            cwd: result.cwd,
            resolvedCwd: result.resolvedCwd,
            output: result.output,
            exitCode: result.exitCode,
            durationMs: result.durationMs,
            timedOut: result.timedOut,
            executedAtUtc: result.executedAtUtc,
          },
        ])
      } catch (err) {
        const message = err instanceof Error ? err.message : 'Unknown error'
        setHistory((prev) => [
          ...prev,
          {
            id: nextId.current++,
            command: trimmed,
            shell: requestShell,
            cwd: requestCwd,
            resolvedCwd: requestCwd,
            output: `Error: ${message}`,
            exitCode: 1,
            durationMs: 0,
            timedOut: false,
            executedAtUtc: new Date().toISOString(),
          },
        ])
      } finally {
        setTimeout(() => inputRef.current?.focus(), 0)
      }
    },
    [input, shell, cwd, execMutation, config, handleClear],
  )

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLInputElement>) => {
      if (e.key === 'ArrowUp') {
        e.preventDefault()
        if (commandHistory.length === 0) return
        if (historyIndex === -1) {
          setDraft(input)
          const last = commandHistory.length - 1
          setHistoryIndex(last)
          setInput(commandHistory[last] ?? '')
        } else if (historyIndex > 0) {
          const next = historyIndex - 1
          setHistoryIndex(next)
          setInput(commandHistory[next] ?? '')
        }
      } else if (e.key === 'ArrowDown') {
        e.preventDefault()
        if (historyIndex === -1) return
        if (historyIndex === commandHistory.length - 1) {
          setHistoryIndex(-1)
          setInput(draft)
        } else {
          const next = historyIndex + 1
          setHistoryIndex(next)
          setInput(commandHistory[next] ?? '')
        }
      } else if (e.key === 'l' && e.ctrlKey) {
        e.preventDefault()
        handleClear()
      }
    },
    [commandHistory, historyIndex, input, draft, handleClear],
  )

  if (configLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    )
  }

  if (configError) {
    return (
      <div className="mx-auto max-w-3xl rounded-2xl border border-down/40 bg-down/10 p-8 text-center">
        <Icon name="error" className="text-3xl text-down" />
        <p className="mt-3 text-sm text-text">Failed to load terminal configuration.</p>
        <p className="mt-1 text-xs text-muted">Check that the API is reachable at /api/terminal/config.</p>
      </div>
    )
  }

  if (config && !config.enabled) {
    return (
      <div className="mx-auto max-w-3xl rounded-2xl border border-warn/40 bg-warn/10 p-8 text-center">
        <Icon name="block" className="text-3xl text-warn" />
        <p className="mt-3 text-sm font-medium text-text">Terminal is disabled</p>
        <p className="mt-1 text-xs text-muted">Set Terminal:Enabled to true in appsettings.json to enable.</p>
      </div>
    )
  }

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-4">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Terminal</h1>
          <p className="mt-1 text-sm text-muted">
            Execute commands on the host. Each command runs in{' '}
            <span className="font-mono text-xs text-text">{cwd || '…'}</span>
          </p>
        </div>
        <div className="flex items-center gap-2">
          <label className="text-xs font-medium text-muted" htmlFor="terminal-shell">
            Shell
          </label>
          <select
            id="terminal-shell"
            value={shell}
            onChange={(e) => setShellOverride(e.target.value)}
            className="cursor-pointer rounded-lg border border-border bg-surface px-3 py-1.5 text-sm text-text outline-none focus:border-accent"
          >
            {allowedShells.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
          <button
            type="button"
            onClick={handleClear}
            className="cursor-pointer rounded-lg border border-border px-3 py-1.5 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
            title="Clear (Ctrl+L / clear)"
          >
            <span className="inline-flex items-center gap-1">
              <Icon name="cleaning_services" className="text-[16px]" />
              Clear
            </span>
          </button>
        </div>
      </div>

      <div className="overflow-hidden rounded-2xl border border-border bg-[#0a0a0a] shadow-xl shadow-black/30">
        <div className="flex items-center justify-between border-b border-border/60 bg-surface px-4 py-2.5">
          <div className="flex items-center gap-2">
            <span className="size-3 rounded-full bg-down/80" aria-hidden="true" />
            <span className="size-3 rounded-full bg-warn/80" aria-hidden="true" />
            <span className="size-3 rounded-full bg-up/80" aria-hidden="true" />
            <span className="ml-3 hidden font-mono text-xs text-muted sm:inline">
              {config?.defaultWorkingDirectory} — {shell}
              <span className="ml-2 text-muted/60">timeout {config?.timeoutSeconds}s · max {Math.round((config?.maxOutputBytes ?? 100000) / 1000)} kB</span>
            </span>
            <span className="ml-3 font-mono text-xs text-muted sm:hidden">
              {shell} · {config?.timeoutSeconds}s
            </span>
          </div>
          <div className="hidden items-center gap-2 text-xs text-muted/70 sm:flex">
            <span className="rounded bg-surface-2 px-1.5 py-0.5 font-mono">↑↓</span> history
            <span className="rounded bg-surface-2 px-1.5 py-0.5 font-mono">clear</span>
          </div>
        </div>

        {/* eslint-disable-next-line jsx-a11y/click-events-have-key-events, jsx-a11y/no-static-element-interactions */}
        <div
          ref={scrollRef}
          onClick={focusInput}
          className="h-[min(68vh,560px)] cursor-text overflow-auto bg-[#0a0a0a] p-4 font-mono text-[13px] leading-relaxed"
        >
          {history.length === 0 && !execMutation.isPending && (
            <div className="select-none space-y-1 py-2 text-muted/70">
              <p className="text-accent">MyHomeLab Terminal — {shell} on {cwd || '…'}</p>
              <p>Type a command and press Enter. Try “help” or “cd ..”.</p>
              <p className="text-xs opacity-70">Output is truncated at {config?.maxOutputBytes} bytes. State (cwd) persists per tab.</p>
            </div>
          )}

          {history.map((entry) => (
            <div key={entry.id} className="mb-3">
              <div className="flex flex-wrap items-baseline gap-2">
                <span className="shrink-0 text-accent">{entry.cwd}&gt;</span>
                <span className="break-all text-text">{entry.command}</span>
                <span
                  className={`ml-auto shrink-0 rounded px-1.5 py-0.5 text-[11px] font-medium ${
                    entry.exitCode === 0 && !entry.timedOut
                      ? 'bg-up/15 text-up'
                      : 'bg-down/15 text-down'
                  }`}
                >
                  {formatExit(entry.exitCode, entry.timedOut)} · {entry.durationMs}ms
                </span>
              </div>
              {entry.output ? (
                <pre className="mt-1 max-h-80 overflow-auto whitespace-pre-wrap break-words rounded-lg bg-white/[0.04] px-3 py-2 text-muted">
                  {entry.output}
                </pre>
              ) : (
                <div className="mt-1 text-xs text-muted/40">(no output)</div>
              )}
            </div>
          ))}

          {execMutation.isPending && (
            <div className="flex items-center gap-2 py-2 text-muted">
              <span className="block size-3 animate-spin rounded-full border-2 border-muted/30 border-t-accent" />
              <span className="font-mono text-xs">Running…</span>
            </div>
          )}
        </div>

        <form
          onSubmit={handleSubmit}
          className="flex items-center gap-2 border-t border-border/60 bg-surface px-3 py-2.5"
        >
          <span className="hidden shrink-0 font-mono text-sm text-accent sm:inline" aria-hidden="true">
            {cwd}&gt;
          </span>
          <span className="shrink-0 font-mono text-sm text-accent sm:hidden" aria-hidden="true">
            &gt;
          </span>
          <input
            ref={(el) => {
              inputRef.current = el
              // callback ref focuses on mount and on every re-render that keeps the element
              // (fixes focus loss when parent re-renders, see SO 22573494)
              if (el && !execMutation.isPending) {
                // use queueMicrotask to avoid stealing focus during pending
                queueMicrotask(() => {
                  if (document.activeElement !== el) el.focus()
                })
              }
            }}
            autoFocus
            value={input}
            onChange={(e) => {
              setInput(e.target.value)
              if (historyIndex !== -1) setHistoryIndex(-1)
            }}
            onKeyDown={handleKeyDown}
            onFocus={(e) => {
              // ensure caret at end when refocused via history navigation
              const len = e.currentTarget.value.length
              try {
                e.currentTarget.setSelectionRange(len, len)
              } catch {
                // ignore
              }
            }}
            placeholder={execMutation.isPending ? 'Running…' : 'Type a command…'}
            readOnly={execMutation.isPending}
            autoComplete="off"
            autoCapitalize="off"
            autoCorrect="off"
            spellCheck={false}
            className="min-w-0 flex-1 bg-transparent font-mono text-sm text-text placeholder:text-muted/40 outline-none read-only:opacity-60"
          />
          <button
            type="submit"
            disabled={execMutation.isPending || !input.trim()}
            onMouseDown={(e) => e.preventDefault()}
            className="shrink-0 cursor-pointer rounded-lg bg-accent px-3.5 py-1.5 text-sm font-semibold text-white transition hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-40"
          >
            Run
          </button>
        </form>
      </div>

      <div className="rounded-xl border border-border/60 bg-surface px-4 py-3 text-xs leading-relaxed text-muted">
        <p className="font-medium text-text">Notes</p>
        <ul className="mt-1 list-disc space-y-0.5 pl-4">
          <li>
            Commands run as the service account on this host (Windows). Prefer read-only or scoped operations unless you
            intend to mutate the machine.
          </li>
          <li>
            <span className="font-mono text-text">cd</span> is handled locally so the working directory persists across executions in this tab.
          </li>
          <li>
            Output includes both stdout and stderr, truncated at{' '}
            <span className="font-mono text-text">{config?.maxOutputBytes} bytes</span>. Long-running commands are killed after{' '}
            <span className="font-mono text-text">{config?.timeoutSeconds}s</span>.
          </li>
        </ul>
      </div>
    </div>
  )
}
