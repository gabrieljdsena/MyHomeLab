import { useEffect, useRef, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { downloadUrl, listFiles, type FileEntry } from '../../api/files'
import { queryKeys } from '../../lib/queryKeys'
import { Icon } from '../../components/Icon'
import {
  clampOpacity,
  DEFAULT_APPEARANCE,
  MAX_OPACITY,
  MIN_OPACITY,
  sanitizeImageUrl,
  WALLPAPER_PRESETS,
  withWallpaper,
  type TerminalAppearance,
  type Wallpaper,
} from './appearance'

const IMAGE_EXTS = new Set(['png', 'jpg', 'jpeg', 'gif', 'webp', 'bmp', 'avif'])

function parentOf(path: string): string {
  const idx = path.lastIndexOf('/')
  return idx <= 0 ? '' : path.slice(0, idx)
}

interface TerminalAppearancePopoverProps {
  appearance: TerminalAppearance
  onChange: (next: TerminalAppearance) => void
}

export function TerminalAppearancePopover({ appearance, onChange }: TerminalAppearancePopoverProps) {
  const [open, setOpen] = useState(false)
  const [browsing, setBrowsing] = useState(false)
  const [path, setPath] = useState('')
  const [urlDraft, setUrlDraft] = useState('')
  const [urlError, setUrlError] = useState<string | null>(null)
  const rootRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    const onDown = (e: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [open])

  const filesQuery = useQuery({
    queryKey: queryKeys.fileList(path),
    queryFn: () => listFiles(path),
    enabled: open && browsing,
    staleTime: 30_000,
    retry: false,
  })

  const selectedPresetId = appearance.wallpaper.kind === 'preset' ? appearance.wallpaper.presetId : null
  const selectedImageUrl = appearance.wallpaper.kind === 'image' ? appearance.wallpaper.url : null
  const wallpaperActive = appearance.wallpaper.kind !== 'none'

  const select = (wallpaper: Wallpaper) => {
    setUrlError(null)
    onChange(withWallpaper(appearance, wallpaper))
  }

  const applyUrl = () => {
    const url = sanitizeImageUrl(urlDraft)
    if (!url) {
      setUrlError('Use an https:// url or a same-origin path like /api/files/download?path=…')
      return
    }
    setUrlDraft('')
    select({ kind: 'image', url })
  }

  const entries = filesQuery.data?.entries ?? []
  const directories = entries.filter((entry) => entry.isDirectory)
  const images = entries.filter((entry) => !entry.isDirectory && entry.extension && IMAGE_EXTS.has(entry.extension))

  return (
    <div className="relative" ref={rootRef}>
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-label="Wallpaper & transparency"
        className={`cursor-pointer rounded-md p-1.5 transition hover:bg-surface-2 hover:text-text ${
          open ? 'bg-surface-2 text-text' : 'text-muted'
        }`}
        title="Wallpaper & transparency"
      >
        <Icon name="palette" className="block text-[16px]" />
      </button>

      {open && (
        <div
          role="dialog"
          aria-label="Terminal appearance"
          className="absolute right-0 z-40 mt-2 w-80 rounded-xl border border-border bg-surface p-3 shadow-xl shadow-black/40"
        >
          <div className="flex items-center justify-between">
            <p className="text-xs font-semibold uppercase tracking-widest text-muted">Wallpaper</p>
            <button
              type="button"
              onClick={() => onChange(DEFAULT_APPEARANCE)}
              className="cursor-pointer rounded px-1.5 py-0.5 text-xs text-muted transition hover:bg-surface-2 hover:text-text"
            >
              Reset
            </button>
          </div>

          <div className="mt-2 grid grid-cols-2 gap-2">
            <button
              type="button"
              onClick={() => select({ kind: 'none' })}
              className={`cursor-pointer overflow-hidden rounded-lg border text-left transition ${
                !wallpaperActive ? 'border-accent' : 'border-border hover:border-muted'
              }`}
            >
              <span className="flex h-12 items-center justify-center bg-[#0a0a0a]">
                <Icon name="block" className="text-[18px] text-muted" />
              </span>
              <span className="block px-2 py-1 text-xs text-text">None</span>
            </button>
            {WALLPAPER_PRESETS.map((preset) => (
              <button
                key={preset.id}
                type="button"
                onClick={() => select({ kind: 'preset', presetId: preset.id })}
                className={`cursor-pointer overflow-hidden rounded-lg border text-left transition ${
                  selectedPresetId === preset.id ? 'border-accent' : 'border-border hover:border-muted'
                }`}
              >
                <span
                  className="block h-12 bg-cover bg-center"
                  style={{ backgroundImage: preset.backgroundImage }}
                  aria-hidden="true"
                />
                <span className="block px-2 py-1 text-xs text-text">{preset.name}</span>
              </button>
            ))}
          </div>

          <div className="mt-3 rounded-lg border border-border/70 p-2">
            <p className="text-xs font-medium text-text">Custom image</p>
            <div className="mt-1.5 flex items-center gap-1.5">
              <input
                type="text"
                value={urlDraft}
                onChange={(e) => {
                  setUrlDraft(e.target.value)
                  setUrlError(null)
                }}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') applyUrl()
                }}
                placeholder="https://… or /api/files/download?path=…"
                className="min-w-0 flex-1 rounded-md border border-border bg-background px-2 py-1 text-xs text-text outline-none placeholder:text-muted/50 focus:border-accent"
              />
              <button
                type="button"
                onClick={applyUrl}
                disabled={!urlDraft.trim()}
                className="shrink-0 cursor-pointer rounded-md bg-accent px-2 py-1 text-xs font-semibold text-white transition hover:bg-accent-hover disabled:cursor-not-allowed disabled:opacity-40"
              >
                Set
              </button>
              <button
                type="button"
                onClick={() => setBrowsing((value) => !value)}
                className={`shrink-0 cursor-pointer rounded-md border px-2 py-1 text-xs transition ${
                  browsing ? 'border-accent text-accent' : 'border-border text-muted hover:text-text'
                }`}
                title="Pick an image from Files"
              >
                Files
              </button>
            </div>
            {urlError && <p className="mt-1 text-[11px] text-down">{urlError}</p>}
            {selectedImageUrl && (
              <p className="mt-1 truncate font-mono text-[11px] text-accent" title={selectedImageUrl}>
                {selectedImageUrl}
              </p>
            )}

            {browsing && (
              <div className="mt-2 rounded-md border border-border/70 bg-background">
                <div className="flex items-center justify-between border-b border-border/60 px-2 py-1">
                  <button
                    type="button"
                    onClick={() => setPath(parentOf(path))}
                    disabled={!path}
                    className="cursor-pointer rounded px-1 text-muted transition hover:text-text disabled:cursor-not-allowed disabled:opacity-30"
                    title="Up one level"
                  >
                    <Icon name="arrow_upward" className="text-[14px]" />
                  </button>
                  <span className="truncate font-mono text-[11px] text-muted">{path || 'Shared-Server'}</span>
                </div>
                <div className="max-h-40 overflow-y-auto">
                  {filesQuery.isLoading && <p className="px-2 py-1.5 text-[11px] text-muted">Loading…</p>}
                  {filesQuery.isError && (
                    <p className="px-2 py-1.5 text-[11px] text-down">Can't list Files (disabled or unreachable).</p>
                  )}
                  {filesQuery.isSuccess && directories.length === 0 && images.length === 0 && (
                    <p className="px-2 py-1.5 text-[11px] text-muted">No images in this folder.</p>
                  )}
                  {directories.map((entry: FileEntry) => (
                    <button
                      key={entry.path}
                      type="button"
                      onClick={() => setPath(entry.path)}
                      className="flex w-full cursor-pointer items-center gap-1.5 px-2 py-1 text-left text-xs text-muted transition hover:bg-surface-2 hover:text-text"
                    >
                      <Icon name="folder" className="text-[14px]" />
                      <span className="truncate">{entry.name}</span>
                    </button>
                  ))}
                  {images.map((entry: FileEntry) => (
                    <button
                      key={entry.path}
                      type="button"
                      onClick={() => select({ kind: 'image', url: downloadUrl(entry.path) })}
                      className={`flex w-full cursor-pointer items-center gap-1.5 px-2 py-1 text-left text-xs transition hover:bg-surface-2 hover:text-text ${
                        selectedImageUrl === downloadUrl(entry.path) ? 'text-accent' : 'text-text'
                      }`}
                    >
                      <Icon name="image" className="text-[14px]" />
                      <span className="truncate">{entry.name}</span>
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>

          <div className="mt-3">
            <div className="flex items-center justify-between">
              <p className="text-xs font-medium text-text">Transparency</p>
              <span className="font-mono text-[11px] text-muted">{appearance.opacity}%</span>
            </div>
            <input
              type="range"
              min={MIN_OPACITY}
              max={MAX_OPACITY}
              step={5}
              value={clampOpacity(appearance.opacity)}
              disabled={!wallpaperActive}
              onChange={(e) => onChange({ ...appearance, opacity: Number(e.target.value) })}
              className="mt-1.5 w-full cursor-pointer accent-accent disabled:cursor-not-allowed disabled:opacity-40"
            />
            <p className="mt-0.5 text-[11px] text-muted">
              {wallpaperActive
                ? 'Terminal background opacity — lower shows more wallpaper.'
                : 'Pick a wallpaper to enable transparency.'}
            </p>
          </div>
        </div>
      )}
    </div>
  )
}
