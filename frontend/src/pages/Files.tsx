import { useMemo, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { Icon, Spinner } from '../components/Icon'
import { downloadUrl, uploadFiles, type FileEntry } from '../api/files'
import {
  useDeletePath,
  useFileList,
  useFilesConfig,
  useMkdir,
  useRename,
} from '../features/files/useFiles'
import { queryKeys } from '../lib/queryKeys'

const IMAGE_EXTS = new Set(['png', 'jpg', 'jpeg', 'gif', 'webp', 'svg'])
const VIDEO_EXTS = new Set(['mp4', 'webm', 'mkv'])
const TEXT_EXTS = new Set(['txt', 'log', 'md', 'json'])

function formatBytes(bytes: number): string {
  if (bytes <= 0) return '—'
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString()
  } catch {
    return iso
  }
}

function iconFor(entry: FileEntry): string {
  if (entry.isDirectory) return 'folder'
  const ext = entry.extension ?? ''
  if (IMAGE_EXTS.has(ext)) return 'image'
  if (VIDEO_EXTS.has(ext)) return 'movie'
  if (TEXT_EXTS.has(ext)) return 'description'
  if (ext === 'pdf') return 'picture_as_pdf'
  if (ext === 'zip' || ext === 'rar' || ext === '7z') return 'folder_zip'
  if (ext === 'apk') return 'android'
  if (ext === 'mp3' || ext === 'wav' || ext === 'ogg') return 'audio_file'
  return 'insert_drive_file'
}

function parentOf(path: string): string {
  const idx = path.lastIndexOf('/')
  return idx <= 0 ? '' : path.slice(0, idx)
}

function crumbs(path: string): { label: string; path: string }[] {
  if (!path) return []
  const parts = path.split('/').filter(Boolean)
  return parts.map((part, i) => ({
    label: part,
    path: parts.slice(0, i + 1).join('/'),
  }))
}

export function Files() {
  const [cwd, setCwd] = useState('')
  const [search, setSearch] = useState('')
  const [mkdirOpen, setMkdirOpen] = useState(false)
  const [mkdirName, setMkdirName] = useState('')
  const [renaming, setRenaming] = useState<FileEntry | null>(null)
  const [renameValue, setRenameValue] = useState('')
  const [deleting, setDeleting] = useState<FileEntry | null>(null)
  const [deleteRecursive, setDeleteRecursive] = useState(false)
  const [preview, setPreview] = useState<FileEntry | null>(null)
  const [dragging, setDragging] = useState(false)
  const [dropTarget, setDropTarget] = useState<string | null>(null)
  const [draggedPath, setDraggedPath] = useState<string | null>(null)
  const [uploading, setUploading] = useState(false)
  const [uploadLabel, setUploadLabel] = useState('')
  const [uploadPercent, setUploadPercent] = useState<number | null>(null)
  const [notice, setNotice] = useState<{ kind: 'error' | 'info'; text: string } | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const queryClient = useQueryClient()

  const configQuery = useFilesConfig()
  const listQuery = useFileList(cwd)
  const mkdirMutation = useMkdir(cwd)
  const renameMutation = useRename()
  const deleteMutation = useDeletePath(cwd)

  const entries = useMemo(() => {
    const all = listQuery.data?.entries ?? []
    const q = search.trim().toLowerCase()
    if (!q) return all
    return all.filter((e) => e.name.toLowerCase().includes(q))
  }, [listQuery.data, search])

  const quotaUsed = listQuery.data?.quotaUsedBytes ?? 0
  const quotaMax = listQuery.data?.quotaMaxBytes ?? configQuery.data?.quotaMaxBytes ?? 0
  const quotaPercent = quotaMax > 0 ? Math.min(100, (quotaUsed / quotaMax) * 100) : 0

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: queryKeys.fileList(cwd) })
  }

  const openEntry = (entry: FileEntry) => {
    if (entry.isDirectory) {
      setCwd(entry.path)
      setSearch('')
      setPreview(null)
      return
    }
    setPreview(entry)
  }

  const runUpload = async (files: FileList | File[], overwrite: boolean, targetDir: string = cwd) => {
    if (files.length === 0) return
    const destLabel = targetDir === '' ? 'the shared root' : `'${targetDir}'`
    setUploading(true)
    setUploadPercent(0)
    setUploadLabel(`Uploading ${files.length} file${files.length === 1 ? '' : 's'} to ${destLabel}…`)
    setNotice(null)
    try {
      await uploadFiles(targetDir, files, {
        overwrite,
        onProgress: ({ loaded, total }) => {
          if (total) setUploadPercent(Math.round((loaded / total) * 100))
          else setUploadPercent(null)
        },
      })
      setNotice({ kind: 'info', text: `Uploaded ${files.length} file${files.length === 1 ? '' : 's'} to ${destLabel}.` })
      void queryClient.invalidateQueries({ queryKey: queryKeys.files })
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Upload failed.'
      const status = (err as Error & { status?: number }).status
      if (status === 409 && !overwrite) {
        const retry = window.confirm(
          `A file with the same name already exists in ${destLabel}.\n\n${message}\n\nPress OK to overwrite, Cancel to keep both (rename your file first).`,
        )
        if (retry) {
          await runUpload(files, true, targetDir)
          return
        }
      }
      setNotice({ kind: 'error', text: message })
    } finally {
      setUploading(false)
      setUploadPercent(null)
      setUploadLabel('')
    }
  }

  const moveEntryToDir = (from: string, toDir: string) => {
    const name = from.slice(from.lastIndexOf('/') + 1)
    const to = toDir ? `${toDir}/${name}` : name
    if (!name || to === from) return
    renameMutation.mutate(
      { from, to },
      {
        onSuccess: (moved) => {
          if (moved.isDirectory && preview && (preview.path === from || preview.path.startsWith(`${from}/`))) {
            setPreview(null)
          } else if (!moved.isDirectory && preview?.path === from) {
            setPreview(moved)
          }
          void queryClient.invalidateQueries({ queryKey: queryKeys.files })
        },
        onError: (err) => {
          setNotice({ kind: 'error', text: err instanceof Error ? err.message : 'Move failed.' })
        },
      },
    )
  }

  const confirmMkdir = () => {
    const name = mkdirName.trim()
    if (!name) return
    mkdirMutation.mutate(
      { parentPath: cwd, name },
      {
        onSuccess: () => {
          setMkdirOpen(false)
          setMkdirName('')
        },
        onError: (err) => {
          setNotice({ kind: 'error', text: err instanceof Error ? err.message : 'Could not create folder.' })
        },
      },
    )
  }

  const openRename = (entry: FileEntry) => {
    setRenaming(entry)
    setRenameValue(entry.path)
  }

  const confirmRename = () => {
    if (!renaming) return
    const to = renameValue.trim().replace(/^\/+/, '')
    if (!to || to === renaming.path) {
      setRenaming(null)
      return
    }
    renameMutation.mutate(
      { from: renaming.path, to },
      {
        onSuccess: (moved) => {
          setRenaming(null)
          if (moved.isDirectory && preview && preview.path.startsWith(renaming.path)) {
            setPreview(null)
          } else if (!moved.isDirectory && preview?.path === renaming.path) {
            setPreview(moved)
          }
          if (parentOf(to) !== cwd) setCwd(parentOf(to))
          else refresh()
        },
        onError: (err) => {
          setNotice({ kind: 'error', text: err instanceof Error ? err.message : 'Rename failed.' })
        },
      },
    )
  }

  const confirmDelete = (recursive: boolean) => {
    if (!deleting) return
    deleteMutation.mutate(
      { target: deleting.path, recursive },
      {
        onSuccess: () => {
          if (preview && (preview.path === deleting.path || preview.path.startsWith(`${deleting.path}/`))) {
            setPreview(null)
          }
          setDeleting(null)
          setDeleteRecursive(false)
        },
        onError: (err) => {
          const message = err instanceof Error ? err.message : 'Delete failed.'
          if (!recursive && message.toLowerCase().includes('not empty')) {
            setDeleteRecursive(true)
            return
          }
          setNotice({ kind: 'error', text: message })
        },
      },
    )
  }

  if (configQuery.isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    )
  }

  if (configQuery.isError) {
    return (
      <div className="mx-auto max-w-md rounded-2xl border border-down/40 bg-down/10 p-8 text-center">
        <Icon name="error" className="text-3xl text-down" />
        <p className="mt-3 text-sm text-text">Failed to load file server configuration.</p>
        <p className="mt-1 text-xs text-muted">Check that the API is reachable at /api/files/config.</p>
      </div>
    )
  }

  if (configQuery.data && !configQuery.data.enabled) {
    return (
      <div className="mx-auto max-w-3xl rounded-2xl border border-warn/40 bg-warn/10 p-8 text-center">
        <Icon name="block" className="text-3xl text-warn" />
        <p className="mt-3 text-sm font-medium text-text">File server is disabled</p>
        <p className="mt-1 text-xs text-muted">Set FileServer:Enabled to true in appsettings.json to enable.</p>
      </div>
    )
  }

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">File Server</h1>
          <p className="mt-1 text-sm text-muted">
            {configQuery.data?.rootName ?? 'Shared'} · {formatBytes(quotaUsed)} of {formatBytes(quotaMax)} used
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            disabled={uploading}
            className="flex cursor-pointer items-center gap-1.5 rounded-lg bg-accent px-3.5 py-2 text-sm font-semibold text-white transition hover:bg-accent-hover disabled:opacity-50"
          >
            <Icon name="upload" className="text-[18px]" />
            Upload
          </button>
          <button
            type="button"
            onClick={() => setMkdirOpen(true)}
            className="flex cursor-pointer items-center gap-1.5 rounded-lg border border-border px-3.5 py-2 text-sm text-text transition hover:bg-surface-2"
          >
            <Icon name="create_new_folder" className="text-[18px]" />
            New folder
          </button>
          <input
            ref={fileInputRef}
            type="file"
            multiple
            className="hidden"
            onChange={(e) => {
              const files = e.target.files
              if (files && files.length > 0) void runUpload(files, false)
              e.target.value = ''
            }}
          />
        </div>
      </div>

      <div className="rounded-2xl border border-border bg-surface px-4 py-3">
        <div className="flex items-center justify-between text-xs text-muted">
          <span>Quota</span>
          <span className="font-mono">
            {formatBytes(quotaUsed)} / {formatBytes(quotaMax)} · {quotaPercent.toFixed(1)}%
          </span>
        </div>
        <div className="mt-2 h-2 overflow-hidden rounded-full bg-surface-2">
          <div
            className={`h-full rounded-full transition-all ${quotaPercent > 90 ? 'bg-down' : quotaPercent > 75 ? 'bg-warn' : 'bg-accent'}`}
            style={{ width: `${quotaPercent}%` }}
          />
        </div>
      </div>

      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <nav aria-label="Breadcrumb" className="flex min-w-0 flex-wrap items-center gap-1 text-sm">
          <button
            type="button"
            onClick={() => {
              setCwd('')
              setPreview(null)
            }}
            onDragOver={(e) => {
              if (Array.from(e.dataTransfer.types).includes('application/x-myhomelab-path')) {
                e.preventDefault()
                e.dataTransfer.dropEffect = 'move'
                setDropTarget('')
              }
            }}
            onDragLeave={() => {
              setDropTarget((current) => (current === '' ? null : current))
            }}
            onDrop={(e) => {
              e.preventDefault()
              e.stopPropagation()
              setDropTarget(null)
              const internal = e.dataTransfer.getData('application/x-myhomelab-path')
              if (internal) moveEntryToDir(internal, '')
            }}
            className={`cursor-pointer rounded-lg px-2 py-1 transition ${cwd === '' ? 'bg-accent-soft text-accent' : 'text-muted hover:bg-surface-2 hover:text-text'} ${dropTarget === '' ? 'ring-2 ring-accent' : ''}`}
            title="Shared root"
          >
            <span className="inline-flex items-center gap-1">
              <Icon name="home" className="text-[16px]" />
              {configQuery.data?.rootName ?? 'Shared'}
            </span>
          </button>
          {crumbs(cwd).map((crumb) => (
            <span key={crumb.path} className="inline-flex min-w-0 items-center gap-1">
              <span className="text-muted/50">/</span>
              <button
                type="button"
                onClick={() => {
                  setCwd(crumb.path)
                  setPreview(null)
                }}
                onDragOver={(e) => {
                  if (Array.from(e.dataTransfer.types).includes('application/x-myhomelab-path')) {
                    e.preventDefault()
                    e.dataTransfer.dropEffect = 'move'
                    setDropTarget(crumb.path)
                  }
                }}
                onDragLeave={() => {
                  setDropTarget((current) => (current === crumb.path ? null : current))
                }}
                onDrop={(e) => {
                  e.preventDefault()
                  e.stopPropagation()
                  setDropTarget(null)
                  const internal = e.dataTransfer.getData('application/x-myhomelab-path')
                  if (internal) moveEntryToDir(internal, crumb.path)
                }}
                className={`max-w-40 cursor-pointer truncate rounded-lg px-2 py-1 text-muted transition hover:bg-surface-2 hover:text-text ${dropTarget === crumb.path ? 'ring-2 ring-accent' : ''}`}
                title={crumb.path}
              >
                {crumb.label}
              </button>
            </span>
          ))}
        </nav>
        <div className="relative">
          <Icon name="search" className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-[18px] text-muted" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Filter this folder…"
            className="w-full rounded-lg border border-border bg-surface py-2 pr-3 pl-9 text-sm text-text outline-none placeholder:text-muted/50 focus:border-accent sm:w-56"
          />
        </div>
      </div>

      {notice && (
        <div
          className={`flex items-center justify-between gap-3 rounded-xl border px-4 py-2.5 text-sm ${
            notice.kind === 'error' ? 'border-down/40 bg-down/10 text-text' : 'border-up/30 bg-up/10 text-text'
          }`}
        >
          <span className="inline-flex items-center gap-2">
            <Icon name={notice.kind === 'error' ? 'error' : 'check_circle'} className={`text-[18px] ${notice.kind === 'error' ? 'text-down' : 'text-up'}`} />
            {notice.text}
          </span>
          <button
            type="button"
            onClick={() => setNotice(null)}
            className="cursor-pointer rounded p-1 text-muted transition hover:text-text"
            aria-label="Dismiss"
          >
            <Icon name="close" className="text-[16px]" />
          </button>
        </div>
      )}

      {(uploading || uploadPercent !== null) && (
        <div className="rounded-xl border border-border bg-surface px-4 py-3">
          <div className="flex items-center gap-2 text-sm">
            <span className="block size-3.5 animate-spin rounded-full border-2 border-muted/30 border-t-accent" />
            <span className="text-text">{uploadLabel || 'Uploading…'}</span>
            {uploadPercent !== null && <span className="ml-auto font-mono text-xs text-muted">{uploadPercent}%</span>}
          </div>
          <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-surface-2">
            <div
              className="h-full rounded-full bg-upload transition-all"
              style={{ width: `${uploadPercent ?? 0}%` }}
            />
          </div>
        </div>
      )}

      {/* eslint-disable-next-line jsx-a11y/no-static-element-interactions */}
      <div
        onDragOver={(e) => {
          if (!Array.from(e.dataTransfer.types).includes('Files')) return
          e.preventDefault()
          setDragging(true)
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => {
          e.preventDefault()
          setDragging(false)
          // Drops on a folder row are handled by the row itself (stopPropagation).
          if (e.dataTransfer.getData('application/x-myhomelab-path')) return
          if (e.dataTransfer.files.length > 0) void runUpload(e.dataTransfer.files, false)
        }}
        className={`overflow-hidden rounded-2xl border bg-surface transition ${dragging ? 'border-accent shadow-lg shadow-accent/20' : 'border-border'}`}
      >
        {cwd !== '' && (
          <button
            type="button"
            onClick={() => {
              setCwd(parentOf(cwd))
              setPreview(null)
            }}
            className="flex w-full cursor-pointer items-center gap-2 border-b border-border/60 px-4 py-2.5 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
          >
            <Icon name="arrow_upward" className="text-[18px]" />
            Up one level
          </button>
        )}

        {listQuery.isLoading ? (
          <div className="flex h-48 items-center justify-center">
            <Spinner />
          </div>
        ) : listQuery.isError ? (
          <div className="p-8 text-center">
            <p className="text-sm text-text">Failed to list this folder.</p>
            <button
              type="button"
              onClick={() => listQuery.refetch()}
              className="mt-3 cursor-pointer rounded-lg border border-border px-4 py-2 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
            >
              Retry
            </button>
          </div>
        ) : entries.length === 0 ? (
          <div className="p-10 text-center">
            <Icon name={dragging ? 'file_upload' : 'folder_open'} className="text-4xl text-muted/50" />
            <p className="mt-3 text-sm text-text">{dragging ? 'Drop files to upload' : 'This folder is empty'}</p>
            <p className="mt-1 text-xs text-muted">
              {search
                ? 'No names match your filter.'
                : 'Drag files here or use Upload. Drop files onto a folder to upload inside it; drag a row onto a folder to move it.'}
            </p>
          </div>
        ) : (
          <ul className="divide-y divide-border/60">
            {entries.map((entry) => (
              <li
                key={entry.path}
                draggable
                onDragStart={(e) => {
                  e.dataTransfer.setData('application/x-myhomelab-path', entry.path)
                  e.dataTransfer.effectAllowed = 'move'
                  setDraggedPath(entry.path)
                }}
                onDragEnd={() => {
                  setDraggedPath(null)
                  setDropTarget(null)
                }}
                onDragOver={
                  entry.isDirectory
                    ? (e) => {
                        const types = Array.from(e.dataTransfer.types)
                        if (!types.includes('Files') && !types.includes('application/x-myhomelab-path')) return
                        e.preventDefault()
                        e.dataTransfer.dropEffect = types.includes('Files') ? 'copy' : 'move'
                        if (entry.path !== draggedPath) setDropTarget(entry.path)
                      }
                    : undefined
                }
                onDragLeave={() => {
                  setDropTarget((current) => (current === entry.path ? null : current))
                }}
                onDrop={
                  entry.isDirectory
                    ? (e) => {
                        e.preventDefault()
                        e.stopPropagation()
                        setDropTarget(null)
                        const internal = e.dataTransfer.getData('application/x-myhomelab-path')
                        if (internal) {
                          moveEntryToDir(internal, entry.path)
                          return
                        }
                        if (e.dataTransfer.files.length > 0) void runUpload(e.dataTransfer.files, false, entry.path)
                      }
                    : undefined
                }
                className={`flex items-center gap-3 px-4 py-2.5 transition hover:bg-surface-2/50 ${dropTarget === entry.path ? 'bg-accent-soft ring-2 ring-accent ring-inset' : ''} ${draggedPath === entry.path ? 'opacity-40' : ''}`}
                title={entry.isDirectory ? 'Drop files here to upload into this folder, or drop a row to move it' : undefined}
              >
                <span className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-surface-2">
                  <Icon name={iconFor(entry)} className="text-[20px] text-accent" />
                </span>
                <button
                  type="button"
                  onClick={() => openEntry(entry)}
                  className="min-w-0 flex-1 cursor-pointer text-left"
                  title={entry.isDirectory ? `Open ${entry.name}` : `Preview ${entry.name}`}
                >
                  <span className="block truncate text-sm font-medium text-text">{entry.name}</span>
                  <span className="mt-0.5 block text-xs text-muted">
                    {entry.isDirectory ? 'Folder' : formatBytes(entry.sizeBytes)} · {formatDate(entry.modifiedAtUtc)}
                  </span>
                </button>
                {!entry.isDirectory && (
                  <a
                    href={downloadUrl(entry.path)}
                    download={entry.name}
                    onClick={(e) => e.stopPropagation()}
                    className="cursor-pointer rounded-lg border border-border p-2 text-muted transition hover:bg-surface-2 hover:text-text"
                    title={`Download ${entry.name}`}
                    aria-label={`Download ${entry.name}`}
                  >
                    <Icon name="download" className="text-[18px]" />
                  </a>
                )}
                <button
                  type="button"
                  onClick={() => openRename(entry)}
                  className="cursor-pointer rounded-lg border border-border p-2 text-muted transition hover:bg-surface-2 hover:text-text"
                  title={`Rename or move ${entry.name}`}
                  aria-label={`Rename ${entry.name}`}
                >
                  <Icon name="edit" className="text-[18px]" />
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setDeleteRecursive(false)
                    setDeleting(entry)
                  }}
                  className="cursor-pointer rounded-lg border border-border p-2 text-muted transition hover:bg-down/10 hover:text-down"
                  title={`Delete ${entry.name}`}
                  aria-label={`Delete ${entry.name}`}
                >
                  <Icon name="delete" className="text-[18px]" />
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      {preview && !preview.isDirectory && (
        <div className="overflow-hidden rounded-2xl border border-border bg-surface">
          <div className="flex items-center justify-between gap-2 border-b border-border/60 px-4 py-2.5">
            <span className="min-w-0 truncate text-sm font-medium">{preview.name}</span>
            <span className="flex shrink-0 items-center gap-2">
              <a
                href={downloadUrl(preview.path)}
                download={preview.name}
                className="flex cursor-pointer items-center gap-1 rounded-lg bg-accent px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-accent-hover"
              >
                <Icon name="download" className="text-[16px]" />
                Download
              </a>
              <button
                type="button"
                onClick={() => setPreview(null)}
                className="cursor-pointer rounded-lg border border-border p-1.5 text-muted transition hover:bg-surface-2 hover:text-text"
                aria-label="Close preview"
              >
                <Icon name="close" className="text-[16px]" />
              </button>
            </span>
          </div>
          <div className="p-4">
            {IMAGE_EXTS.has(preview.extension ?? '') ? (
              <img
                src={downloadUrl(preview.path)}
                alt={preview.name}
                className="mx-auto max-h-[60vh] rounded-xl object-contain"
              />
            ) : VIDEO_EXTS.has(preview.extension ?? '') ? (
              <video src={downloadUrl(preview.path)} controls preload="metadata" className="mx-auto max-h-[60vh] w-full rounded-xl" />
            ) : (
              <p className="text-sm text-muted">
                No inline preview for this file type ({preview.extension ?? 'unknown'} · {formatBytes(preview.sizeBytes)}).
                Use Download to open it locally.
              </p>
            )}
          </div>
        </div>
      )}

      {mkdirOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm" onClick={() => setMkdirOpen(false)}>
          <div
            className="w-full max-w-sm rounded-2xl border border-border bg-surface p-6"
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
            aria-label="New folder"
          >
            <h3 className="text-base font-semibold">New folder</h3>
            <p className="mt-1 text-xs text-muted">Inside {cwd === '' ? (configQuery.data?.rootName ?? 'Shared') : cwd}</p>
            <input
              autoFocus
              value={mkdirName}
              onChange={(e) => setMkdirName(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') confirmMkdir()
              }}
              placeholder="Folder name"
              className="mt-4 w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none placeholder:text-muted/50 focus:border-accent"
            />
            <div className="mt-6 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setMkdirOpen(false)}
                className="cursor-pointer rounded-lg border border-border px-4 py-2 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={confirmMkdir}
                disabled={mkdirMutation.isPending || !mkdirName.trim()}
                className="cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-hover disabled:opacity-50"
              >
                {mkdirMutation.isPending ? 'Creating…' : 'Create'}
              </button>
            </div>
          </div>
        </div>
      )}

      {renaming && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm" onClick={() => setRenaming(null)}>
          <div
            className="w-full max-w-md rounded-2xl border border-border bg-surface p-6"
            onClick={(e) => e.stopPropagation()}
            role="dialog"
            aria-modal="true"
            aria-label="Rename or move"
          >
            <h3 className="text-base font-semibold">Rename or move</h3>
            <p className="mt-1 text-xs text-muted">
              Edit the path relative to the shared root. Changing folders moves the item.
            </p>
            <input
              autoFocus
              value={renameValue}
              onChange={(e) => setRenameValue(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') confirmRename()
              }}
              className="mt-4 w-full rounded-lg border border-border bg-surface-2 px-3 py-2 font-mono text-sm text-text outline-none focus:border-accent"
            />
            <div className="mt-6 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setRenaming(null)}
                className="cursor-pointer rounded-lg border border-border px-4 py-2 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={confirmRename}
                disabled={renameMutation.isPending}
                className="cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-hover disabled:opacity-50"
              >
                {renameMutation.isPending ? 'Saving…' : 'Save'}
              </button>
            </div>
          </div>
        </div>
      )}

      <ConfirmDialog
        open={deleting !== null}
        title={deleteRecursive ? 'Delete folder and everything inside?' : `Delete ${deleting?.isDirectory ? 'folder' : 'file'}?`}
        message={
          deleting
            ? deleteRecursive
              ? `'${deleting.path}' and all of its contents will be permanently deleted. This cannot be undone.`
              : deleting.isDirectory
                ? `'${deleting.path}' will be deleted. If it is not empty you will be asked to confirm recursive delete.`
                : `'${deleting.path}' (${formatBytes(deleting.sizeBytes)}) will be permanently deleted.`
            : ''
        }
        confirmLabel={deleteMutation.isPending ? 'Deleting…' : deleteRecursive ? 'Delete everything' : 'Delete'}
        busy={deleteMutation.isPending}
        onConfirm={() => confirmDelete(deleteRecursive)}
        onCancel={() => {
          if (!deleteMutation.isPending) {
            setDeleting(null)
            setDeleteRecursive(false)
          }
        }}
      />
    </div>
  )
}
