import { afterEach, describe, expect, it, vi } from 'vitest'
import { buildPtyUrl } from './useTerminalSocket'

describe('buildPtyUrl', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('builds a ws url with shell and size', () => {
    vi.stubGlobal('window', { location: { protocol: 'https:', host: 'hub.local' } })

    expect(buildPtyUrl('cmd', 80, 24)).toBe('wss://hub.local/api/terminal/ws?shell=cmd&cols=80&rows=24')
  })

  it('uses ws on http and appends the session when reattaching', () => {
    vi.stubGlobal('window', { location: { protocol: 'http:', host: 'hub.local:8080' } })

    const url = buildPtyUrl('powershell', 120, 30, 'a1b2c3')
    expect(url.startsWith('ws://hub.local:8080/api/terminal/ws?')).toBe(true)
    expect(url).toContain('session=a1b2c3')
  })

  it('rounds fractional sizes', () => {
    vi.stubGlobal('window', { location: { protocol: 'https:', host: 'hub.local' } })

    expect(buildPtyUrl('cmd', 80.6, 23.4)).toContain('cols=81&rows=23')
  })
})
