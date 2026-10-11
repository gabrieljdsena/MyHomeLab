import { useEffect, useRef, useState } from 'react'
import { Terminal as XTerm } from '@xterm/xterm'
import { FitAddon } from '@xterm/addon-fit'
import { WebLinksAddon } from '@xterm/addon-web-links'
import '@xterm/xterm/css/xterm.css'
import { useTerminalSocket } from './useTerminalSocket'
import { Icon } from '../../components/Icon'
import type { TerminalConfig } from '../../api/terminal'

interface TerminalPtyProps {
  shell: string
  shells: string[]
  config: TerminalConfig
  onShellChange: (shell: string) => void
  onReconnect: () => void
}

interface Notice {
  kind: 'error' | 'exit' | 'info'
  text: string
}

export function TerminalPty({ shell, shells, config, onShellChange, onReconnect }: TerminalPtyProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const termRef = useRef<XTerm | null>(null)
  const [notice, setNotice] = useState<Notice | null>(null)
  const [killed, setKilled] = useState(false)

  const { status, error, connect, disconnect, sendInput, sendResize } = useTerminalSocket({
    shell,
    onOutput: (data) => {
      termRef.current?.write(data)
    },
    onExit: (info) => {
      setNotice({
        kind: 'exit',
        text: info.code === null ? 'Session ended.' : `Session ended (exit ${info.code}).`,
      })
    },
    onErrorMessage: (message) => {
      setNotice({ kind: 'error', text: message })
    },
  })

  // Mount once per session (parent remounts via key on shell change / reconnect).
  useEffect(() => {
    const container = containerRef.current
    if (!container) return

    const term = new XTerm({
      cursorBlink: true,
      fontSize: 13,
      fontFamily: "ui-monospace, 'Cascadia Mono', 'JetBrains Mono', Menlo, Consolas, monospace",
      scrollback: 5000,
      theme: {
        background: '#0a0a0a',
        foreground: '#e8e8e8',
        cursor: '#f97316',
        cursorAccent: '#0a0a0a',
        selectionBackground: 'rgba(249, 115, 22, 0.35)',
        black: '#0a0a0a',
        red: '#f87171',
        green: '#4ade80',
        yellow: '#facc15',
        blue: '#60a5fa',
        magenta: '#c084fc',
        cyan: '#22d3ee',
        white: '#e8e8e8',
        brightBlack: '#525252',
        brightRed: '#f87171',
        brightGreen: '#4ade80',
        brightYellow: '#facc15',
        brightBlue: '#60a5fa',
        brightMagenta: '#c084fc',
        brightCyan: '#22d3ee',
        brightWhite: '#ffffff',
      },
    })
    const fit = new FitAddon()
    term.loadAddon(fit)
    term.loadAddon(new WebLinksAddon())
    term.open(container)
    termRef.current = term

    const sendInputRef = { current: true }
    const dataDispose = term.onData((data) => {
      if (sendInputRef.current) sendInput(data)
    })

    const fitAndReport = () => {
      try {
        fit.fit()
      } catch {
        // container not laid out yet; dimensions follow on next resize
      }
      sendResize(term.cols || 120, term.rows || 30)
    }

    // Connect with the fitted size (fallback to classic 120x30).
    try {
      fit.fit()
    } catch {
      // ignore; connect with fallback below
    }
    const dims = fit.proposeDimensions()
    connect(dims?.cols ?? 120, dims?.rows ?? 30)

    const observer = new ResizeObserver(fitAndReport)
    observer.observe(container)
    term.focus()

    return () => {
      sendInputRef.current = false
      observer.disconnect()
      dataDispose.dispose()
      disconnect()
      termRef.current = null
      term.dispose()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const handleClear = () => {
    termRef.current?.clear()
    termRef.current?.focus()
  }

  const handleKill = () => {
    disconnect()
    setKilled(true)
    setNotice({ kind: 'info', text: 'Session killed. The shell process was terminated.' })
  }

  const statusDot =
    status === 'open' ? 'bg-up' : status === 'connecting' ? 'bg-warn animate-pulse' : status === 'error' ? 'bg-down' : 'bg-muted'
  const statusText =
    status === 'open'
      ? `connected · ${shell}`
      : status === 'connecting'
        ? 'connecting…'
        : status === 'error'
          ? 'connection error'
          : 'disconnected'

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Terminal</h1>
          <p className="mt-1 flex items-center gap-2 text-sm text-muted">
            <span className={`inline-block size-2 rounded-full ${statusDot}`} aria-hidden="true" />
            {statusText}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <label className="text-xs font-medium text-muted" htmlFor="terminal-shell">
            Shell
          </label>
          <select
            id="terminal-shell"
            value={shell}
            onChange={(e) => onShellChange(e.target.value)}
            className="cursor-pointer rounded-lg border border-border bg-surface px-3 py-1.5 text-sm text-text outline-none focus:border-accent"
          >
            {shells.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
          <button
            type="button"
            onClick={onReconnect}
            className="cursor-pointer rounded-lg border border-border px-3 py-1.5 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
            title="Start a new session"
          >
            <span className="inline-flex items-center gap-1">
              <Icon name="restart_alt" className="text-[16px]" />
              Reconnect
            </span>
          </button>
          <button
            type="button"
            onClick={handleClear}
            className="cursor-pointer rounded-lg border border-border px-3 py-1.5 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
            title="Clear (Ctrl+L)"
          >
            <span className="inline-flex items-center gap-1">
              <Icon name="cleaning_services" className="text-[16px]" />
              Clear
            </span>
          </button>
          {!killed && status === 'open' && (
            <button
              type="button"
              onClick={handleKill}
              className="cursor-pointer rounded-lg border border-down/40 px-3 py-1.5 text-sm text-down transition hover:bg-down/10"
              title="Terminate this shell"
            >
              Kill
            </button>
          )}
        </div>
      </div>

      {(notice ?? (error ? { kind: 'error' as const, text: error } : null)) && (
        <div
          className={`flex flex-wrap items-center gap-3 rounded-xl border px-4 py-2.5 text-sm ${
            (notice?.kind ?? 'error') === 'error'
              ? 'border-down/40 bg-down/10 text-down'
              : (notice?.kind ?? 'error') === 'exit'
                ? 'border-border bg-surface text-muted'
                : 'border-warn/40 bg-warn/10 text-warn'
          }`}
        >
          <span className="min-w-0 flex-1">{(notice ?? { text: error ?? '' }).text}</span>
          {(notice?.kind === 'exit' || killed || status === 'closed') && (
            <button
              type="button"
              onClick={onReconnect}
              className="shrink-0 cursor-pointer rounded-lg bg-accent px-3 py-1 text-xs font-semibold text-white transition hover:bg-accent-hover"
            >
              New session
            </button>
          )}
        </div>
      )}

      <div className="overflow-hidden rounded-2xl border border-border bg-[#0a0a0a] shadow-xl shadow-black/30">
        <div className="flex items-center justify-between border-b border-border/60 bg-surface px-4 py-2.5">
          <div className="flex items-center gap-2">
            <span className="size-3 rounded-full bg-down/80" aria-hidden="true" />
            <span className="size-3 rounded-full bg-warn/80" aria-hidden="true" />
            <span className="size-3 rounded-full bg-up/80" aria-hidden="true" />
            <span className="ml-3 hidden font-mono text-xs text-muted sm:inline">
              {shell} · interactive pty
              <span className="ml-2 text-muted/60">
                idle {Math.round(config.ptyIdleTimeoutSeconds / 60)} min · max {config.ptyMaxSessions} sessions
              </span>
            </span>
            <span className="ml-3 font-mono text-xs text-muted sm:hidden">{shell}</span>
          </div>
          <div className="hidden items-center gap-2 text-xs text-muted/70 sm:flex">
            <span className="rounded bg-surface-2 px-1.5 py-0.5 font-mono">Ctrl+L</span> clear
          </div>
        </div>

        {/* eslint-disable-next-line jsx-a11y/click-events-have-key-events, jsx-a11y/no-static-element-interactions */}
        <div
          ref={containerRef}
          onClick={() => termRef.current?.focus()}
          className="h-[min(68vh,560px)] cursor-text overflow-hidden bg-[#0a0a0a] p-2 [&_.xterm]:h-full [&_.xterm-viewport]:overflow-y-auto"
        />
      </div>

      <div className="rounded-xl border border-border/60 bg-surface px-4 py-3 text-xs leading-relaxed text-muted">
        <p className="font-medium text-text">Notes</p>
        <ul className="mt-1 list-disc space-y-0.5 pl-4">
          <li>
            Fully interactive shell on this host (Windows) — tab completion, colors, <span className="font-mono text-text">Ctrl+C</span>,
            and full-screen apps like <span className="font-mono text-text">opencode</span> work here.
          </li>
          <li>Sessions idle longer than {Math.round(config.ptyIdleTimeoutSeconds / 60)} minutes are closed automatically.</li>
          <li>Commands run as the service account — prefer read-only operations unless you intend to mutate the machine.</li>
        </ul>
      </div>
    </div>
  )
}
