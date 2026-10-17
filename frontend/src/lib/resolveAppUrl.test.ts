import { describe, expect, it } from 'vitest'
import { resolveAppUrl } from './resolveAppUrl'

describe('resolveAppUrl', () => {
  it('replaces the host while keeping scheme and port', () => {
    expect(resolveAppUrl('http://192.168.15.22:5051', 'dell-server1')).toBe(
      'http://dell-server1:5051/',
    )
  })

  it('keeps the https scheme', () => {
    expect(resolveAppUrl('https://192.168.15.22:8443', 'dell-server1')).toBe(
      'https://dell-server1:8443/',
    )
  })

  it('keeps path and query string', () => {
    expect(resolveAppUrl('https://192.168.15.22:8443/romm?tab=games', 'dell-server1')).toBe(
      'https://dell-server1:8443/romm?tab=games',
    )
  })

  it('handles URLs without an explicit port', () => {
    expect(resolveAppUrl('http://192.168.15.22', 'dell-server1')).toBe('http://dell-server1/')
  })

  it('returns the original string when the host already matches', () => {
    expect(resolveAppUrl('http://192.168.15.22:5051', '192.168.15.22')).toBe(
      'http://192.168.15.22:5051',
    )
    expect(resolveAppUrl('https://dell-server1.tail1f7af5.ts.net:8443', 'dell-server1.tail1f7af5.ts.net')).toBe(
      'https://dell-server1.tail1f7af5.ts.net:8443',
    )
  })

  it('returns the input unchanged when it is not a valid URL', () => {
    expect(resolveAppUrl('not a url', 'dell-server1')).toBe('not a url')
    expect(resolveAppUrl('', 'dell-server1')).toBe('')
  })
})
