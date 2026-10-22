import { describe, expect, it } from 'vitest'
import {
  clampOpacity,
  DEFAULT_APPEARANCE,
  MAX_OPACITY,
  MIN_OPACITY,
  parseAppearance,
  sanitizeImageUrl,
  serializeAppearance,
  terminalBackground,
  wallpaperStyle,
  withWallpaper,
  WALLPAPER_PRESETS,
  type TerminalAppearance,
} from './appearance'

describe('sanitizeImageUrl', () => {
  it('accepts same-origin paths and https urls', () => {
    expect(sanitizeImageUrl('/api/files/download?path=bg.png')).toBe('/api/files/download?path=bg.png')
    expect(sanitizeImageUrl('https://example.com/bg.jpg')).toBe('https://example.com/bg.jpg')
    expect(sanitizeImageUrl('  /wallpapers/a.png  ')).toBe('/wallpapers/a.png')
  })

  it('rejects protocol-relative, javascript and empty values', () => {
    expect(sanitizeImageUrl('//evil.example/bg.png')).toBeNull()
    expect(sanitizeImageUrl('javascript:alert(1)')).toBeNull()
    expect(sanitizeImageUrl('http://example.com/bg.jpg')).toBeNull()
    expect(sanitizeImageUrl('')).toBeNull()
  })
})

describe('clampOpacity', () => {
  it('clamps to the allowed range and rounds', () => {
    expect(clampOpacity(5)).toBe(MIN_OPACITY)
    expect(clampOpacity(150)).toBe(MAX_OPACITY)
    expect(clampOpacity(72.4)).toBe(72)
    expect(clampOpacity(Number.NaN)).toBe(MAX_OPACITY)
  })
})

describe('withWallpaper', () => {
  it('drops opacity to 80 when enabling a wallpaper from full opacity', () => {
    const next = withWallpaper(DEFAULT_APPEARANCE, { kind: 'preset', presetId: 'aurora' })

    expect(next.wallpaper).toEqual({ kind: 'preset', presetId: 'aurora' })
    expect(next.opacity).toBe(80)
  })

  it('keeps a custom opacity untouched', () => {
    const custom: TerminalAppearance = { wallpaper: { kind: 'none' }, opacity: 60 }
    const next = withWallpaper(custom, { kind: 'preset', presetId: 'aurora' })

    expect(next.opacity).toBe(60)
  })
})

describe('wallpaperStyle', () => {
  it('resolves known presets, rejects unknown ones', () => {
    const preset = WALLPAPER_PRESETS[0]
    expect(wallpaperStyle({ kind: 'preset', presetId: preset.id })).toEqual({
      backgroundImage: preset.backgroundImage,
    })
    expect(wallpaperStyle({ kind: 'preset', presetId: 'nope' })).toBeNull()
  })

  it('quotes image urls and returns null for none', () => {
    expect(wallpaperStyle({ kind: 'image', url: '/api/files/download?x=1' })).toEqual({
      backgroundImage: 'url("/api/files/download?x=1")',
    })
    expect(wallpaperStyle({ kind: 'none' })).toBeNull()
  })
})

describe('terminalBackground', () => {
  it('maps opacity to an rgba alpha', () => {
    expect(terminalBackground(100)).toBe('rgba(10, 10, 10, 1)')
    expect(terminalBackground(75)).toBe('rgba(10, 10, 10, 0.75)')
    expect(terminalBackground(1)).toBe(`rgba(10, 10, 10, ${MIN_OPACITY / 100})`)
  })
})

describe('parseAppearance / serializeAppearance', () => {
  it('round-trips an appearance', () => {
    const appearance: TerminalAppearance = {
      wallpaper: { kind: 'image', url: 'https://example.com/bg.jpg' },
      opacity: 65,
    }

    expect(parseAppearance(serializeAppearance(appearance))).toEqual(appearance)
  })

  it('falls back to defaults for garbage input', () => {
    expect(parseAppearance(null)).toEqual(DEFAULT_APPEARANCE)
    expect(parseAppearance('{not json')).toEqual(DEFAULT_APPEARANCE)
    expect(parseAppearance('{"wallpaper":{"kind":"preset","presetId":"unknown"},"opacity":500}')).toEqual({
      wallpaper: { kind: 'none' },
      opacity: MAX_OPACITY,
    })
    expect(parseAppearance('{"wallpaper":{"kind":"image","url":"javascript:alert(1)"}}')).toEqual({
      wallpaper: { kind: 'none' },
      opacity: DEFAULT_APPEARANCE.opacity,
    })
  })
})
