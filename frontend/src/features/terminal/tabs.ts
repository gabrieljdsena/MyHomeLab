export interface PtyTab {
  id: number
  shell: string
  nonce: number
  sessionId?: string
}

export interface TabsState {
  tabs: PtyTab[]
  activeId: number | null
}

export const EMPTY_TABS: TabsState = { tabs: [], activeId: null }

const STORAGE_KEY = 'myhomelab.terminal-tabs'

export function createTab(id: number, shell: string, sessionId?: string): PtyTab {
  return sessionId ? { id, shell, nonce: 0, sessionId } : { id, shell, nonce: 0 }
}

export function canAddTab(state: TabsState, maxSessions: number): boolean {
  return state.tabs.length < Math.max(1, maxSessions)
}

export function addTab(state: TabsState, id: number, shell: string, maxSessions: number): TabsState {
  if (!canAddTab(state, maxSessions)) return state
  return { tabs: [...state.tabs, createTab(id, shell)], activeId: id }
}

export function activateTab(state: TabsState, id: number): TabsState {
  if (state.activeId === id || !state.tabs.some((tab) => tab.id === id)) return state
  return { ...state, activeId: id }
}

export function closeTab(state: TabsState, id: number): TabsState {
  const index = state.tabs.findIndex((tab) => tab.id === id)
  if (index === -1) return state

  const tabs = state.tabs.filter((tab) => tab.id !== id)
  if (state.activeId !== id) return { tabs, activeId: state.activeId }

  const next = tabs[Math.min(index, tabs.length - 1)]
  return { tabs, activeId: next ? next.id : null }
}

export function reconnectTab(state: TabsState, id: number): TabsState {
  return {
    ...state,
    tabs: state.tabs.map((tab) =>
      tab.id === id ? { ...tab, nonce: tab.nonce + 1, sessionId: undefined } : tab,
    ),
  }
}

export function setTabShell(state: TabsState, id: number, shell: string): TabsState {
  return {
    ...state,
    tabs: state.tabs.map((tab) => (tab.id === id ? { ...tab, shell } : tab)),
  }
}

export function setTabSession(state: TabsState, id: number, sessionId: string): TabsState {
  const tab = state.tabs.find((candidate) => candidate.id === id)
  if (!tab || tab.sessionId === sessionId) return state
  return {
    ...state,
    tabs: state.tabs.map((candidate) => (candidate.id === id ? { ...candidate, sessionId } : candidate)),
  }
}

export function attachTab(
  state: TabsState,
  id: number,
  shell: string,
  sessionId: string,
  maxSessions: number,
): TabsState {
  const next = addTab(state, id, shell, maxSessions)
  return next === state ? state : setTabSession(next, id, sessionId)
}

export function tabTitle(tabs: PtyTab[], tab: PtyTab): string {
  const sameShell = tabs.filter((candidate) => candidate.shell === tab.shell)
  if (sameShell.length <= 1) return tab.shell
  const ordinal = sameShell.findIndex((candidate) => candidate.id === tab.id) + 1
  return `${tab.shell} ${ordinal}`
}

export function sanitizeTabsState(value: unknown): TabsState | null {
  if (typeof value !== 'object' || value === null) return null
  const raw = value as { tabs?: unknown; activeId?: unknown }
  if (!Array.isArray(raw.tabs)) return null

  const tabs: PtyTab[] = []
  for (const entry of raw.tabs) {
    if (typeof entry !== 'object' || entry === null) continue
    const tab = entry as { id?: unknown; shell?: unknown; sessionId?: unknown }
    if (typeof tab.id !== 'number' || !Number.isInteger(tab.id)) continue
    if (typeof tab.shell !== 'string' || !tab.shell) continue
    const sessionId = typeof tab.sessionId === 'string' && tab.sessionId ? tab.sessionId : undefined
    tabs.push(createTab(tab.id, tab.shell, sessionId))
  }

  if (tabs.length === 0) return null
  const activeId =
    typeof raw.activeId === 'number' && tabs.some((tab) => tab.id === raw.activeId)
      ? raw.activeId
      : tabs[0].id
  return { tabs, activeId }
}

export function loadTabsState(): TabsState | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    return sanitizeTabsState(JSON.parse(raw))
  } catch {
    return null
  }
}

export function saveTabsState(state: TabsState): void {
  try {
    const payload = {
      tabs: state.tabs.map(({ id, shell, sessionId }) => ({ id, shell, sessionId })),
      activeId: state.activeId,
    }
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(payload))
  } catch {
    // Storage can be unavailable (private mode); persistence is best effort.
  }
}
