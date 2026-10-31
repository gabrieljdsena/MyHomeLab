export type Wallpaper =
  | { kind: 'none' }
  | { kind: 'preset'; presetId: string }
  | { kind: 'image'; url: string }

export interface TerminalAppearance {
  wallpaper: Wallpaper
  opacity: number
}

export interface WallpaperPreset {
  id: string
  name: string
  backgroundImage: string
}

export const WALLPAPER_PRESETS: readonly WallpaperPreset[] = [
  {
    id: 'ember',
    name: 'Ember',
    backgroundImage:
      'radial-gradient(120% 90% at 85% 0%, rgba(249, 115, 22, 0.55) 0%, rgba(124, 45, 18, 0.5) 40%, #0c0c0c 100%)',
  },
  {
    id: 'aurora',
    name: 'Aurora',
    backgroundImage: 'linear-gradient(135deg, #052e16 0%, #065f46 45%, #0e7490 100%)',
  },
  {
    id: 'nebula',
    name: 'Nebula',
    backgroundImage: 'radial-gradient(110% 100% at 15% 10%, #4c1d95 0%, #1e1b4b 50%, #0c0c0c 100%)',
  },
  {
    id: 'graphite',
    name: 'Graphite',
    backgroundImage: 'linear-gradient(160deg, #262626 0%, #111111 55%, #000000 100%)',
  },
]

export const MIN_OPACITY = 20
export const MAX_OPACITY = 100
export const DEFAULT_APPEARANCE: TerminalAppearance = { wallpaper: { kind: 'none' }, opacity: MAX_OPACITY }

const STORAGE_KEY = 'myhomelab.terminal-appearance'

export function sanitizeImageUrl(raw: string): string | null {
  const url = raw.trim()
  if (!url) return null
  if (url.startsWith('/') && !url.startsWith('//')) return url
  if (/^https:\/\//i.test(url)) return url
  return null
}

export function clampOpacity(value: number): number {
  if (!Number.isFinite(value)) return MAX_OPACITY
  return Math.min(MAX_OPACITY, Math.max(MIN_OPACITY, Math.round(value)))
}

export function withWallpaper(appearance: TerminalAppearance, wallpaper: Wallpaper): TerminalAppearance {
  const turnedOn = wallpaper.kind !== 'none' && appearance.wallpaper.kind === 'none'
  return {
    wallpaper,
    opacity: turnedOn && appearance.opacity === MAX_OPACITY ? 80 : appearance.opacity,
  }
}

export function wallpaperStyle(wallpaper: Wallpaper): { backgroundImage: string } | null {
  if (wallpaper.kind === 'preset') {
    const preset = WALLPAPER_PRESETS.find((candidate) => candidate.id === wallpaper.presetId)
    return preset ? { backgroundImage: preset.backgroundImage } : null
  }
  if (wallpaper.kind === 'image') {
    return { backgroundImage: `url("${wallpaper.url}")` }
  }
  return null
}

export function terminalBackground(opacity: number): string {
  return `rgba(10, 10, 10, ${clampOpacity(opacity) / 100})`
}

function parseWallpaper(value: unknown): Wallpaper {
  if (!value || typeof value !== 'object') return { kind: 'none' }
  const record = value as Record<string, unknown>

  if (record.kind === 'preset' && typeof record.presetId === 'string') {
    const presetId = record.presetId
    if (WALLPAPER_PRESETS.some((preset) => preset.id === presetId)) {
      return { kind: 'preset', presetId }
    }
    return { kind: 'none' }
  }

  if (record.kind === 'image' && typeof record.url === 'string') {
    const url = sanitizeImageUrl(record.url)
    return url ? { kind: 'image', url } : { kind: 'none' }
  }

  return { kind: 'none' }
}

export function parseAppearance(raw: string | null): TerminalAppearance {
  if (!raw) return DEFAULT_APPEARANCE
  try {
    const data = JSON.parse(raw) as Record<string, unknown>
    return {
      wallpaper: parseWallpaper(data.wallpaper),
      opacity: typeof data.opacity === 'number' ? clampOpacity(data.opacity) : DEFAULT_APPEARANCE.opacity,
    }
  } catch {
    return DEFAULT_APPEARANCE
  }
}

export function serializeAppearance(appearance: TerminalAppearance): string {
  return JSON.stringify({
    version: 1,
    wallpaper: appearance.wallpaper,
    opacity: clampOpacity(appearance.opacity),
  })
}

export function loadAppearance(): TerminalAppearance {
  try {
    return parseAppearance(window.localStorage.getItem(STORAGE_KEY))
  } catch {
    return DEFAULT_APPEARANCE
  }
}

export function saveAppearance(appearance: TerminalAppearance): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, serializeAppearance(appearance))
  } catch {
    // private mode / quota — appearance just won't persist
  }
}
