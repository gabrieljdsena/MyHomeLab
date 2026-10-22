import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useKillPtySession, useTerminalConfig, useTerminalSessions } from './useTerminal'
import { Icon, Spinner } from '../../components/Icon'
import { TerminalAppearancePopover } from './TerminalAppearancePopover'
import { TerminalExec } from './TerminalExec'
import { TerminalSessionsMenu } from './TerminalSessionsMenu'
import {
  activateTab,
  addTab,
  attachTab,
  canAddTab,
  closeTab,
  createTab,
  loadTabsState,
  reconnectTab,
  saveTabsState,
  setTabSession,
  setTabShell,
  tabTitle,
  EMPTY_TABS,
  type TabsState,
} from './tabs'
import { loadAppearance, saveAppearance, type TerminalAppearance } from './appearance'
import type { PtyStatus } from './useTerminalSocket'
import type { PtySessionSummary } from '../../api/terminal'

const TerminalPty = lazy(() => import('./TerminalPty').then((m) => ({ default: m.TerminalPty })))

function statusDotClass(status: PtyStatus | undefined): string {
  if (status === 'open') return 'bg-up'
  if (status === 'connecting') return 'bg-warn animate-pulse'
  if (status === 'error') return 'bg-down'
  return 'bg-muted'
}

interface TerminalWorkspaceProps {
  visible: boolean
}

export function TerminalWorkspace({ visible }: TerminalWorkspaceProps) {
  const { data: config, isLoading: configLoading, isError: configError } = useTerminalConfig()
  const ptyReady = Boolean(config?.enabled && config?.ptyEnabled)
  const sessionsQuery = useTerminalSessions(ptyReady)
  const killSession = useKillPtySession()
  const killSessionRef = useRef(killSession)
  useEffect(() => {
    killSessionRef.current = killSession
  })

  const [tabsState, setTabsState] = useState<TabsState>(EMPTY_TABS)
  const [statuses, setStatuses] = useState<Record<number, PtyStatus>>({})
  const [appearance, setAppearance] = useState<TerminalAppearance>(() => loadAppearance())
  const [fullscreen, setFullscreen] = useState(false)
  const nextTabIdRef = useRef(1)
  const initializedRef = useRef(false)
  const overlayRef = useRef<HTMLDivElement>(null)

  const allowedShells = useMemo(() => {
    const raw = config?.allowedShells ?? ['powershell', 'cmd']
    return [...new Set(raw.map((s) => s.toLowerCase()))]
  }, [config])

  const sessions = useMemo(() => sessionsQuery.data ?? [], [sessionsQuery.data])
  const liveSessionIds = useMemo(() => new Set(sessions.map((session) => session.id)), [sessions])
  const localSessionIds = useMemo(
    () => new Set(tabsState.tabs.map((tab) => tab.sessionId).filter((id): id is string => Boolean(id))),
    [tabsState.tabs],
  )
  const otherSessions = useMemo(
    () => sessions.filter((session) => !localSessionIds.has(session.id)),
    [sessions, localSessionIds],
  )

  useEffect(() => {
    saveAppearance(appearance)
  }, [appearance])

  // Restore persisted tabs once the live-session list is known: keep tabs whose
  // session still exists, otherwise fall back to a single fresh shell.
  useEffect(() => {
    if (initializedRef.current) return
    if (tabsState.tabs.length > 0) {
      initializedRef.current = true
      return
    }
    if (!config || !config.enabled || !config.ptyEnabled) return
    if (!sessionsQuery.isSuccess) return
    initializedRef.current = true

    const stored = loadTabsState()
    if (stored) {
      const liveTabs = stored.tabs.filter((tab) => !tab.sessionId || liveSessionIds.has(tab.sessionId))
      if (liveTabs.length > 0) {
        nextTabIdRef.current = Math.max(...liveTabs.map((tab) => tab.id)) + 1
        const activeId = liveTabs.some((tab) => tab.id === stored.activeId)
          ? stored.activeId
          : liveTabs[0].id
        // eslint-disable-next-line react/set-state-in-effect -- one-time restore from storage validated against the server session list
        setTabsState({ tabs: liveTabs, activeId })
        return
      }
    }

    const id = nextTabIdRef.current++
    // eslint-disable-next-line react/set-state-in-effect -- seed the first shell once config and sessions are known
    setTabsState({ tabs: [createTab(id, config.defaultShell)], activeId: id })
  }, [config, sessionsQuery.isSuccess, liveSessionIds, tabsState.tabs.length])

  useEffect(() => {
    if (initializedRef.current) saveTabsState(tabsState)
  }, [tabsState])

  useEffect(() => {
    const onFullscreenChange = () => {
      if (!document.fullscreenElement) setFullscreen(false)
    }
    document.addEventListener('fullscreenchange', onFullscreenChange)
    return () => document.removeEventListener('fullscreenchange', onFullscreenChange)
  }, [])

  // Leaving the page keeps the fullscreen state; Layout drops native fullscreen
  // and the hidden wrapper hides the CSS overlay, so re-entering restores both.
  const handleToggleFullscreen = useCallback(() => {
    if (!fullscreen) {
      setFullscreen(true)
    } else {
      setFullscreen(false)
      if (document.fullscreenElement) void document.exitFullscreen().catch(() => {})
    }
  }, [fullscreen])

  // The overlay only exists once fullscreen is set, so request native
  // fullscreen after it mounts (falls back to the CSS overlay if denied).
  useEffect(() => {
    if (!fullscreen || !visible) return
    const element = overlayRef.current
    if (element && !document.fullscreenElement) {
      void element.requestFullscreen().catch(() => {})
    }
  }, [fullscreen, visible])

  const handleAddTab = useCallback(() => {
    if (!config) return
    const id = nextTabIdRef.current++
    setTabsState((prev) => {
      const shell = prev.tabs.find((tab) => tab.id === prev.activeId)?.shell ?? config.defaultShell
      return addTab(prev, id, shell, config.ptyMaxSessions)
    })
  }, [config])

  const handleCloseTab = useCallback(
    (id: number) => {
      const tab = tabsState.tabs.find((candidate) => candidate.id === id)
      if (tab?.sessionId) {
        void killSessionRef.current.mutateAsync(tab.sessionId).catch(() => {})
      }
      setTabsState((prev) => closeTab(prev, id))
      setStatuses((prev) => {
        if (!(id in prev)) return prev
        const next = { ...prev }
        delete next[id]
        return next
      })
    },
    [tabsState.tabs],
  )

  const handleActivateTab = useCallback((id: number) => {
    setTabsState((prev) => activateTab(prev, id))
  }, [])

  const handleReconnectTab = useCallback((id: number) => {
    setTabsState((prev) => reconnectTab(prev, id))
  }, [])

  const handleShellChange = useCallback((id: number, shell: string) => {
    setTabsState((prev) => setTabShell(prev, id, shell))
  }, [])

  const handleTabSession = useCallback((id: number, sessionId: string) => {
    setTabsState((prev) => setTabSession(prev, id, sessionId))
  }, [])

  const handleTabStatus = useCallback((id: number, status: PtyStatus) => {
    setStatuses((prev) => (prev[id] === status ? prev : { ...prev, [id]: status }))
  }, [])

  const handleAttachSession = useCallback(
    (session: PtySessionSummary) => {
      if (!config) return
      const id = nextTabIdRef.current++
      setTabsState((prev) => {
        const next = attachTab(prev, id, session.shell, session.id, config.ptyMaxSessions)
        if (next === prev) nextTabIdRef.current = id
        return next
      })
    },
    [config],
  )

  const handleKillSession = useCallback((id: string) => {
    void killSessionRef.current.mutateAsync(id).catch(() => {})
  }, [])

  if (configLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    )
  }

  if (configError || !config || !config.enabled || !config.ptyEnabled) {
    return <TerminalExec />
  }

  const maxSessions = config.ptyMaxSessions
  const canAdd = canAddTab(tabsState, maxSessions)
  const activeTab = tabsState.tabs.find((tab) => tab.id === tabsState.activeId)

  const terminalShell = (
    <div
      className={`relative flex min-h-0 flex-col rounded-2xl border border-border bg-[#0a0a0a] shadow-xl shadow-black/30 ${
        fullscreen ? 'flex-1' : ''
      }`}
    >
      <div className="relative flex min-h-0 flex-1 flex-col">
        <div className="relative z-20 flex items-center gap-2 rounded-t-2xl border-b border-border/60 bg-surface px-2 py-1.5">
          <div className="flex min-w-0 flex-1 items-center gap-1 overflow-x-auto">
            {tabsState.tabs.map((tab) => {
              const active = tab.id === tabsState.activeId
              const title = tabTitle(tabsState.tabs, tab)
              return (
                <div
                  key={tab.id}
                  className={`flex shrink-0 items-center rounded-lg transition ${
                    active
                      ? 'bg-background/70 text-text shadow-sm ring-1 ring-border'
                      : 'text-muted hover:bg-background/40 hover:text-text'
                  }`}
                >
                  <button
                    type="button"
                    onClick={() => handleActivateTab(tab.id)}
                    className="flex cursor-pointer items-center gap-2 py-1 pl-2.5 pr-1"
                    title={`Switch to ${title}`}
                  >
                    <span className={`size-2 rounded-full ${statusDotClass(statuses[tab.id])}`} aria-hidden="true" />
                    <span className={`font-mono text-xs ${active ? 'text-accent' : ''}`}>{title}</span>
                  </button>
                  <button
                    type="button"
                    onClick={() => handleCloseTab(tab.id)}
                    aria-label={`Close ${title}`}
                    className="cursor-pointer py-1 pl-1 pr-2 text-muted/50 transition hover:text-down"
                    title="Close tab (kills its shell)"
                  >
                    <Icon name="close" className="block text-[13px]" />
                  </button>
                </div>
              )
            })}
            <button
              type="button"
              onClick={handleAddTab}
              disabled={!canAdd}
              className="ml-0.5 shrink-0 cursor-pointer rounded-md p-1 text-muted transition hover:bg-surface-2 hover:text-text disabled:cursor-not-allowed disabled:opacity-40"
              title={canAdd ? 'New tab' : `Session limit reached (${maxSessions})`}
            >
              <Icon name="add" className="block text-[16px]" />
            </button>
          </div>

          <span className="hidden shrink-0 font-mono text-[11px] text-muted sm:inline">
            {tabsState.tabs.length}/{maxSessions} sessions
          </span>
          <TerminalSessionsMenu
            sessions={otherSessions}
            canAttach={canAdd}
            onAttach={handleAttachSession}
            onKill={handleKillSession}
          />
          <TerminalAppearancePopover appearance={appearance} onChange={setAppearance} />
          <button
            type="button"
            onClick={handleToggleFullscreen}
            className="shrink-0 cursor-pointer rounded-md p-1.5 text-muted transition hover:bg-surface-2 hover:text-text"
            title={fullscreen ? 'Exit fullscreen (Esc)' : 'Fullscreen'}
            aria-label={fullscreen ? 'Exit fullscreen' : 'Fullscreen'}
          >
            <Icon name={fullscreen ? 'fullscreen_exit' : 'fullscreen'} className="block text-[16px]" />
          </button>
        </div>

        {tabsState.tabs.length === 0 ? (
          <div className="flex min-h-64 flex-1 flex-col items-center justify-center gap-3 p-6">
            <p className="text-sm text-muted">No terminal open.</p>
            <button
              type="button"
              onClick={handleAddTab}
              className="cursor-pointer rounded-lg bg-accent px-3.5 py-1.5 text-sm font-semibold text-white transition hover:bg-accent-hover"
            >
              New session
            </button>
          </div>
        ) : (
          tabsState.tabs.map((tab) => {
            const active = tab.id === tabsState.activeId
            return (
              <div
                key={`${tab.id}-${tab.shell}-${tab.nonce}`}
                className={active ? (fullscreen ? 'flex min-h-0 flex-1 flex-col' : 'flex flex-col') : 'hidden'}
              >
                <TerminalPty
                  shell={tab.shell}
                  shells={allowedShells}
                  config={config}
                  appearance={appearance}
                  sessionId={tab.sessionId}
                  isActive={active}
                  visible={visible}
                  fullscreen={fullscreen}
                  onShellChange={(shell) => handleShellChange(tab.id, shell)}
                  onReconnect={() => handleReconnectTab(tab.id)}
                  onSessionId={(sessionId) => handleTabSession(tab.id, sessionId)}
                  onStatusChange={(status) => handleTabStatus(tab.id, status)}
                />
              </div>
            )
          })
        )}
      </div>
    </div>
  )

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col">
      <Suspense
        fallback={
          <div className="flex h-64 items-center justify-center">
            <Spinner />
          </div>
        }
      >
        <div className="flex flex-col gap-4">
          {!fullscreen && (
            <div>
              <h1 className="text-xl font-semibold tracking-tight">Terminal</h1>
              <p className="mt-1 text-sm text-muted">
                Interactive shells on this host — sessions keep running across pages, refresh, and other devices until
                you kill them or they go idle.
              </p>
            </div>
          )}

          <div
            ref={overlayRef}
            className={
              fullscreen ? 'fixed inset-0 z-50 flex flex-col bg-background p-3 sm:p-4' : 'flex flex-col'
            }
          >
            {terminalShell}
          </div>

          {!fullscreen && (
            <div className="rounded-xl border border-border/60 bg-surface px-4 py-3 text-xs leading-relaxed text-muted">
              <p className="font-medium text-text">Notes</p>
              <ul className="mt-1 list-disc space-y-0.5 pl-4">
                <li>
                  Fully interactive shells on this host (Windows) — tab completion, colors,{' '}
                  <span className="font-mono text-text">Ctrl+C</span>, and full-screen apps like{' '}
                  <span className="font-mono text-text">opencode</span> work here. Each tab is a separate process;
                  switching tabs keeps its shell running.
                </li>
                <li>
                  Sessions live on the host, not in the browser: they survive page changes and refreshes, and any device
                  can attach to them from the sessions menu. Close a tab (or hit Kill) to end a shell. Sessions idle
                  longer than{' '}
                  <span className="font-mono text-text">{Math.round(config.ptyIdleTimeoutSeconds / 60)} minutes</span>{' '}
                  are closed automatically; up to{' '}
                  <span className="font-mono text-text">{config.ptyMaxSessions}</span> concurrent sessions, and the
                  active tab{' '}
                  <span className="font-mono text-text">{activeTab ? tabTitle(tabsState.tabs, activeTab) : '—'}</span>{' '}
                  is in front.
                </li>
                <li>
                  Wallpaper and transparency live in the Appearance menu and are saved in this browser only (custom
                  images can come from Files or any https URL).
                </li>
                <li>Commands run as the service account — prefer read-only operations unless you intend to mutate the machine.</li>
              </ul>
            </div>
          )}
        </div>
      </Suspense>
    </div>
  )
}
