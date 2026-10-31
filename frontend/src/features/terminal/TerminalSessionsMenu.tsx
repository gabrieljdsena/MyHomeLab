import { useEffect, useRef, useState } from 'react'
import { Icon } from '../../components/Icon'
import type { PtySessionSummary } from '../../api/terminal'

interface TerminalSessionsMenuProps {
  sessions: PtySessionSummary[]
  canAttach: boolean
  onAttach: (session: PtySessionSummary) => void
  onKill: (id: string) => void
}

function relativeTime(iso: string): string {
  const started = Date.parse(iso)
  if (Number.isNaN(started)) return '—'
  const seconds = Math.max(0, Math.round((Date.now() - started) / 1000))
  if (seconds < 60) return `${seconds}s`
  const minutes = Math.round(seconds / 60)
  if (minutes < 60) return `${minutes}m`
  const hours = Math.round(minutes / 60)
  return `${hours}h`
}

export function TerminalSessionsMenu({ sessions, canAttach, onAttach, onKill }: TerminalSessionsMenuProps) {
  const [open, setOpen] = useState(false)
  const rootRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const onDown = (e: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [open])

  return (
    <div className="relative shrink-0" ref={rootRef}>
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-label="Live terminal sessions"
        title="Live sessions on this host (attach from any device)"
        className={`relative cursor-pointer rounded-md p-1.5 transition hover:bg-surface-2 hover:text-text ${
          open ? 'bg-surface-2 text-text' : 'text-muted'
        }`}
      >
        <Icon name="devices" className="block text-[16px]" />
        {sessions.length > 0 && (
          <span className="absolute -right-0.5 -top-0.5 flex size-3.5 items-center justify-center rounded-full bg-accent text-[9px] font-semibold text-white">
            {sessions.length}
          </span>
        )}
      </button>

      {open && (
        <div
          role="dialog"
          aria-label="Live terminal sessions"
          className="absolute right-0 z-40 mt-2 w-80 rounded-xl border border-border bg-surface p-3 shadow-xl shadow-black/40"
        >
          <p className="text-xs font-semibold uppercase tracking-widest text-muted">Live sessions</p>
          <p className="mt-1 text-[11px] text-muted">
            Sessions persist on the host. Attach to one from this device — shared sessions show the same screen everywhere.
          </p>

          {sessions.length === 0 ? (
            <p className="mt-2 rounded-lg border border-border/70 px-2 py-1.5 text-[11px] text-muted">
              No other sessions running.
            </p>
          ) : (
            <div className="mt-2 max-h-56 space-y-1 overflow-y-auto">
              {sessions.map((session) => (
                <div
                  key={session.id}
                  className="flex items-center gap-2 rounded-lg border border-border/70 px-2 py-1.5"
                >
                  <Icon name="terminal" className="shrink-0 text-[14px] text-muted" />
                  <div className="min-w-0 flex-1">
                    <p className="truncate font-mono text-xs text-text">{session.shell}</p>
                    <p className="truncate text-[10px] text-muted" title={session.workingDirectory}>
                      {session.workingDirectory} · {relativeTime(session.startedAtUtc)} · {session.attachedClients}{' '}
                      {session.attachedClients === 1 ? 'client' : 'clients'}
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => {
                      onAttach(session)
                      setOpen(false)
                    }}
                    disabled={!canAttach}
                    className="shrink-0 cursor-pointer rounded-md bg-accent px-2 py-1 text-[11px] font-semibold text-white transition hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-40"
                    title={canAttach ? 'Attach this session as a new tab' : 'Session limit reached'}
                  >
                    Attach
                  </button>
                  <button
                    type="button"
                    onClick={() => onKill(session.id)}
                    className="shrink-0 cursor-pointer rounded-md p-1 text-muted/70 transition hover:bg-down/10 hover:text-down"
                    title="Kill this session"
                    aria-label={`Kill ${session.shell} session`}
                  >
                    <Icon name="delete" className="block text-[15px]" />
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  )
}
