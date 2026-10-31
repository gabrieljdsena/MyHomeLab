import { describe, expect, it } from 'vitest'
import {
  activateTab,
  addTab,
  attachTab,
  canAddTab,
  closeTab,
  createTab,
  EMPTY_TABS,
  reconnectTab,
  sanitizeTabsState,
  setTabSession,
  setTabShell,
  tabTitle,
  type PtyTab,
  type TabsState,
} from './tabs'

const stateOf = (...tabs: PtyTab[]): TabsState => ({ tabs, activeId: tabs[0]?.id ?? null })

describe('addTab', () => {
  it('appends and activates a new tab', () => {
    const state = addTab(EMPTY_TABS, 1, 'powershell', 4)

    expect(state.tabs).toEqual([{ id: 1, shell: 'powershell', nonce: 0 }])
    expect(state.activeId).toBe(1)
  })

  it('refuses to exceed the session cap', () => {
    const full = stateOf(createTab(1, 'cmd'), createTab(2, 'cmd'))

    expect(canAddTab(full, 2)).toBe(false)
    expect(addTab(full, 3, 'cmd', 2)).toBe(full)
  })
})

describe('closeTab', () => {
  it('activates the neighbor when the active tab closes', () => {
    const state = stateOf(createTab(1, 'cmd'), createTab(2, 'cmd'), createTab(3, 'cmd'))
    const next = closeTab({ ...state, activeId: 2 }, 2)

    expect(next.tabs.map((tab) => tab.id)).toEqual([1, 3])
    expect(next.activeId).toBe(3)
  })

  it('keeps the active tab when another closes', () => {
    const state = { tabs: [createTab(1, 'cmd'), createTab(2, 'cmd')], activeId: 2 }
    const next = closeTab(state, 1)

    expect(next.activeId).toBe(2)
  })

  it('falls back to the last tab when the active one is last', () => {
    const state = { tabs: [createTab(1, 'cmd'), createTab(2, 'cmd')], activeId: 2 }
    const next = closeTab(state, 2)

    expect(next.tabs.map((tab) => tab.id)).toEqual([1])
    expect(next.activeId).toBe(1)
  })

  it('clears the active id when the last tab closes', () => {
    const state = { tabs: [createTab(1, 'cmd')], activeId: 1 }
    const next = closeTab(state, 1)

    expect(next.tabs).toEqual([])
    expect(next.activeId).toBeNull()
  })

  it('ignores unknown ids', () => {
    const state = stateOf(createTab(1, 'cmd'))

    expect(closeTab(state, 99)).toBe(state)
  })
})

describe('activateTab', () => {
  it('activates an existing tab and ignores unknown ids', () => {
    const state = stateOf(createTab(1, 'cmd'), createTab(2, 'cmd'))

    expect(activateTab(state, 2).activeId).toBe(2)
    expect(activateTab(state, 99)).toBe(state)
  })
})

describe('reconnectTab / setTabShell', () => {
  it('bumps the nonce for one tab only', () => {
    const state = stateOf(createTab(1, 'cmd'), createTab(2, 'cmd'))
    const next = reconnectTab(state, 2)

    expect(next.tabs[0].nonce).toBe(0)
    expect(next.tabs[1].nonce).toBe(1)
  })

  it('clears the session id so reconnect starts a fresh shell', () => {
    const state = stateOf(createTab(1, 'cmd', 'session-a'))
    const next = reconnectTab(state, 1)

    expect(next.tabs[0].sessionId).toBeUndefined()
  })

  it('replaces the shell of one tab', () => {
    const state = stateOf(createTab(1, 'cmd'), createTab(2, 'cmd'))
    const next = setTabShell(state, 2, 'powershell')

    expect(next.tabs[1].shell).toBe('powershell')
    expect(next.tabs[0].shell).toBe('cmd')
  })
})

describe('setTabSession / attachTab', () => {
  it('stores the session id on the tab', () => {
    const state = stateOf(createTab(1, 'cmd'))
    const next = setTabSession(state, 1, 'session-a')

    expect(next.tabs[0].sessionId).toBe('session-a')
  })

  it('returns the same state for unknown ids and duplicate ids', () => {
    const state = stateOf(createTab(1, 'cmd', 'session-a'))

    expect(setTabSession(state, 99, 'session-b')).toBe(state)
    expect(setTabSession(state, 1, 'session-a')).toBe(state)
  })

  it('attaches an existing session as a new active tab', () => {
    const state = stateOf(createTab(1, 'cmd'))
    const next = attachTab(state, 2, 'powershell', 'remote-session', 4)

    expect(next.tabs.map((tab) => tab.id)).toEqual([1, 2])
    expect(next.activeId).toBe(2)
    expect(next.tabs[1].sessionId).toBe('remote-session')
  })

  it('refuses to attach beyond the session cap', () => {
    const full = stateOf(createTab(1, 'cmd'))

    expect(attachTab(full, 2, 'cmd', 'session-a', 1)).toBe(full)
  })
})

describe('sanitizeTabsState', () => {
  it('accepts valid entries and normalizes missing fields', () => {
    const state = sanitizeTabsState({
      tabs: [
        { id: 1, shell: 'powershell', sessionId: 'session-a' },
        { id: 2, shell: 'cmd' },
      ],
      activeId: 2,
    })

    expect(state).toEqual({
      tabs: [
        { id: 1, shell: 'powershell', nonce: 0, sessionId: 'session-a' },
        { id: 2, shell: 'cmd', nonce: 0 },
      ],
      activeId: 2,
    })
  })

  it('falls back to the first tab for an unknown active id', () => {
    const state = sanitizeTabsState({ tabs: [{ id: 7, shell: 'cmd' }], activeId: 99 })

    expect(state?.activeId).toBe(7)
  })

  it('drops invalid entries and rejects garbage', () => {
    expect(sanitizeTabsState({ tabs: [{ id: 'x', shell: 'cmd' }, { id: 3 }] })).toBeNull()
    expect(sanitizeTabsState({ tabs: [] })).toBeNull()
    expect(sanitizeTabsState({ tabs: 'nope' })).toBeNull()
    expect(sanitizeTabsState(null)).toBeNull()
    expect(sanitizeTabsState('{not json')).toBeNull()
  })
})

describe('tabTitle', () => {
  it('uses the bare shell name when unique', () => {
    const tabs = [createTab(1, 'powershell'), createTab(2, 'cmd')]

    expect(tabTitle(tabs, tabs[1])).toBe('cmd')
  })

  it('numbers duplicates by order', () => {
    const tabs = [createTab(1, 'cmd'), createTab(2, 'powershell'), createTab(3, 'cmd')]

    expect(tabTitle(tabs, tabs[0])).toBe('cmd 1')
    expect(tabTitle(tabs, tabs[2])).toBe('cmd 2')
    expect(tabTitle(tabs, tabs[1])).toBe('powershell')
  })
})
