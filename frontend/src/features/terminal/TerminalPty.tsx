import { useEffect, useId, useRef, useState } from 'react'
import { Terminal as XTerm } from '@xterm/xterm'
import { FitAddon } from '@xterm/addon-fit'
import { WebLinksAddon } from '@xterm/addon-web-links'
import '@xterm/xterm/css/xterm.css'
import { useTerminalSocket, type PtyStatus } from './useTerminalSocket'
import { Icon } from '../../components/Icon'
import { killPtySession, type TerminalConfig } from '../../api/terminal'
import { terminalBackground, wallpaperStyle, type TerminalAppearance } from './appearance'

interface TerminalPtyProps {
  shell: string
  shells: string[]
  config: TerminalConfig
  appearance: TerminalAppearance
  sessionId?: string
  isActive: boolean
  visible: boolean
  fullscreen: boolean
  onShellChange: (shell: string) => void
  onReconnect: () => void
  onSessionId?: (sessionId: string) => void
  onStatusChange?: (status: PtyStatus) => void
}

interface Notice {
  kind: 'error' | 'exit' | 'info'
  text: string
}

const OPAQUE_BACKGROUND = '#0a0a0a'
// xterm paints the theme background on both the viewport and the render layer,
// so an alpha color would stack. When a wallpaper is active the theme goes
// fully transparent and a single tint layer behind the terminal carries the
// opacity; `allowTransparency` is fixed at construction (cannot be toggled
// later without reopening, see xterm docs).
const TRANSPARENT_BACKGROUND = 'rgba(0, 0, 0, 0)'

function terminalTheme(background: string) {
  return {
    background,
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
  }
}

export function TerminalPty({
  shell,
  shells,
  config,
  appearance,
  sessionId,
  isActive,
  visible,
  fullscreen,
  onShellChange,
  onReconnect,
  onSessionId,
  onStatusChange,
}: TerminalPtyProps) {
  const containerRef = useRef<HTMLDivElement>(null)
  const termRef = useRef<XTerm | null>(null)
  const fitRef = useRef<FitAddon | null>(null)
  const [notice, setNotice] = useState<Notice | null>(null)
  const [killed, setKilled] = useState(false)
  const [assignedSessionId, setAssignedSessionId] = useState<string | undefined>(sessionId)
  const shellSelectId = useId()

  const { status, error, connect, disconnect, sendInput, sendResize } = useTerminalSocket({
    shell,
    sessionId,
    onOutput: (data) => {
      termRef.current?.write(data)
    },
    onExit: (info) => {
      setNotice({
        kind: 'exit',
        text: info.code === null ? 'Session ended.' : `Session ended (exit ${info.code}).`,
      })
    },
    onAttached: (id) => {
      setAssignedSessionId(id)
      onSessionId?.(id)
    },
    onErrorMessage: (message) => {
      setNotice({ kind: 'error', text: message })
    },
  })

  const wallpaper = wallpaperStyle(appearance.wallpaper)
  const wallpaperActive = wallpaper !== null
  const themeBackground = wallpaperActive ? TRANSPARENT_BACKGROUND : OPAQUE_BACKGROUND
  const tintAlpha = terminalBackground(appearance.opacity)

  const outputSendRef = useRef({ sendInput, sendResize })
  useEffect(() => {
    outputSendRef.current = { sendInput, sendResize }
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
      allowTransparency: true,
      theme: terminalTheme(themeBackground),
    })
    const fit = new FitAddon()
    term.loadAddon(fit)
    term.loadAddon(new WebLinksAddon())
    term.open(container)
    termRef.current = term
    fitRef.current = fit

    const sendInputRef = { current: true }
    const dataDispose = term.onData((data) => {
      if (sendInputRef.current) outputSendRef.current.sendInput(data)
    })

    const fitAndReport = () => {
      if (container.clientWidth === 0 || container.clientHeight === 0) return
      try {
        fit.fit()
      } catch {
        // container not laid out yet; dimensions follow on next resize
      }
      outputSendRef.current.sendResize(term.cols || 120, term.rows || 30)
    }

    // Connect with the fitted size (fallback to classic 120x30).
    try {
      fit.fit()
    } catch {
      // ignore; connect with fallback below
    }
    const dims = container.clientWidth > 0 ? fit.proposeDimensions() : undefined
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
      fitRef.current = null
      term.dispose()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Wallpaper on/off swaps the theme background; repaint so the tint layer
  // behind the terminal shows through.
  useEffect(() => {
    const term = termRef.current
    if (!term) return
    term.options.theme = terminalTheme(themeBackground)
    term.refresh(0, Math.max(term.rows - 1, 0))
  }, [themeBackground])

  // Hidden tabs keep their socket, so refit when a tab becomes visible or
  // fullscreen changes the pane height.
  useEffect(() => {
    if (!isActive || !visible) return
    const term = termRef.current
    const container = containerRef.current
    if (!term || !container || container.clientWidth === 0) return
    try {
      fitRef.current?.fit()
    } catch {
      // ignore; ResizeObserver retries
    }
    sendResize(term.cols || 120, term.rows || 30)
    term.focus()
  }, [isActive, visible, fullscreen, sendResize])

  const statusRef = useRef(onStatusChange)
  useEffect(() => {
    statusRef.current = onStatusChange
  })
  useEffect(() => {
    statusRef.current?.(status)
  }, [status])

  const handleClear = () => {
    termRef.current?.clear()
    termRef.current?.focus()
  }

  const handleKill = async () => {
    const id = assignedSessionId
    try {
      if (id) await killPtySession(id)
    } catch {
      // Already gone (idle timeout / other client) — local teardown still applies.
    }
    disconnect()
    setKilled(true)
    setNotice({ kind: 'info', text: 'Session killed. The shell process was terminated.' })
  }

  const paneHeight = fullscreen ? 'min-h-0 flex-1' : 'h-[min(68vh,560px)]'
  const iconButtonClass =
    'cursor-pointer rounded-md p-1.5 text-muted transition hover:bg-surface-2 hover:text-text'

  return (
    <div className={`flex flex-col ${fullscreen ? 'min-h-0 flex-1' : ''}`}>
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-border/60 bg-surface px-3 py-1.5">
        <div className="flex min-w-0 items-center gap-2">
          <span className="mr-1 hidden items-center gap-1.5 sm:flex" aria-hidden="true">
            <span className="size-2.5 rounded-full bg-down/80" />
            <span className="size-2.5 rounded-full bg-warn/80" />
            <span className="size-2.5 rounded-full bg-up/80" />
          </span>
          <span className="truncate font-mono text-[11px] text-muted/60">
            {shell} · pty · idle {Math.round(config.ptyIdleTimeoutSeconds / 60)} min · max {config.ptyMaxSessions}{' '}
            sessions
          </span>
        </div>
        <div className="flex items-center gap-1.5">
          <label className="sr-only" htmlFor={shellSelectId}>
            Shell
          </label>
          <select
            id={shellSelectId}
            value={shell}
            onChange={(e) => onShellChange(e.target.value)}
            className="cursor-pointer rounded-md border border-border bg-surface px-2 py-1 text-xs text-text outline-none focus:border-accent"
          >
            {shells.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
          <button type="button" onClick={onReconnect} className={iconButtonClass} title="Reconnect — start a new shell">
            <Icon name="restart_alt" className="block text-[16px]" />
          </button>
          <button type="button" onClick={handleClear} className={iconButtonClass} title="Clear (Ctrl+L)">
            <Icon name="cleaning_services" className="block text-[16px]" />
          </button>
          {!killed && status === 'open' && (
            <button
              type="button"
              onClick={() => void handleKill()}
              className="cursor-pointer rounded-md border border-down/40 px-2 py-1 text-xs text-down transition hover:bg-down/10"
              title="Terminate this shell"
            >
              Kill
            </button>
          )}
        </div>
      </div>

      {(notice ?? (error ? { kind: 'error' as const, text: error } : null)) && (
        <div
          className={`flex flex-wrap items-center gap-3 border-b border-border/60 bg-background/50 px-3 py-2 text-sm ${
            (notice?.kind ?? 'error') === 'error'
              ? 'text-down'
              : (notice?.kind ?? 'error') === 'exit'
                ? 'text-muted'
                : 'text-warn'
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

      <div className={`relative overflow-hidden rounded-b-2xl ${paneHeight}`}>
        {wallpaper && (
          <div
            aria-hidden="true"
            className="pointer-events-none absolute inset-0 bg-cover bg-center bg-no-repeat"
            style={wallpaper}
          />
        )}
        {wallpaperActive && (
          <div aria-hidden="true" className="absolute inset-0" style={{ backgroundColor: tintAlpha }} />
        )}
        {/* eslint-disable-next-line jsx-a11y/click-events-have-key-events, jsx-a11y/no-static-element-interactions */}
        <div
          ref={containerRef}
          onClick={() => termRef.current?.focus()}
          className={`relative h-full cursor-text overflow-hidden p-2 ${
            wallpaperActive ? 'xterm-transparent' : ''
          } [&_.xterm]:h-full [&_.xterm-viewport]:overflow-y-auto`}
        />
      </div>
    </div>
  )
}
