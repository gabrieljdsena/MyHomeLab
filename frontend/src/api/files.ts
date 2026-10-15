import { del, get, post } from './client'
import { chunkPlan, DEFAULT_CHUNK_BYTES } from '../lib/chunks'

export interface FileEntry {
  name: string
  path: string
  isDirectory: boolean
  sizeBytes: number
  modifiedAtUtc: string
  extension: string | null
}

export interface FileListResponse {
  path: string
  entries: FileEntry[]
  quotaUsedBytes: number
  quotaMaxBytes: number
}

export interface FileServerConfig {
  enabled: boolean
  rootName: string
  quotaMaxBytes: number
}

export const getFilesConfig = () => get<FileServerConfig>('/files/config')

export const listFiles = (path: string) =>
  get<FileListResponse>(`/files?path=${encodeURIComponent(path)}`)

export const downloadUrl = (path: string) =>
  `/api/files/download?path=${encodeURIComponent(path)}`

export const mkdir = (parentPath: string, name: string) =>
  post<FileEntry>('/files/mkdir', { parentPath, name })

export const renamePath = (from: string, to: string) =>
  post<FileEntry>('/files/rename', { from, to })

export const deletePath = (path: string, recursive = false) =>
  del<void>(`/files?path=${encodeURIComponent(path)}&recursive=${recursive}`)

export interface UploadProgress {
  loaded: number
  total: number | null
}

export interface UploadSession {
  uploadId: string
  directory: string
  fileName: string
  totalSizeBytes: number
  totalChunks: number
  chunkSizeBytes: number
  receivedChunks: number[]
}

export const initUpload = (
  dir: string,
  fileName: string,
  totalSizeBytes: number,
  totalChunks: number,
  overwrite: boolean,
) =>
  post<UploadSession>('/files/uploads/init', {
    directory: dir,
    fileName,
    totalSizeBytes,
    totalChunks,
    overwrite,
  })

export const getUploadStatus = (uploadId: string) =>
  get<UploadSession>(`/files/uploads/${encodeURIComponent(uploadId)}`)

export const completeUpload = (uploadId: string) =>
  post<FileEntry>(`/files/uploads/${encodeURIComponent(uploadId)}/complete`)

export const abortUpload = (uploadId: string) =>
  del<void>(`/files/uploads/${encodeURIComponent(uploadId)}`)

function problemDetail(xhr: XMLHttpRequest): string {
  let detail = xhr.statusText || `Upload failed (HTTP ${xhr.status}).`
  try {
    const problem = JSON.parse(xhr.responseText) as { title?: string; detail?: string }
    detail = problem.detail ?? problem.title ?? detail
  } catch {
    // keep status text
  }
  return detail
}

function withStatus(message: string, status: number): Error & { status?: number } {
  const error = new Error(message) as Error & { status?: number }
  error.status = status
  return error
}

export function putChunk(
  uploadId: string,
  chunkIndex: number,
  blob: Blob,
  options: {
    onProgress?: (progress: UploadProgress) => void
    signal?: AbortSignal
  } = {},
): Promise<void> {
  return new Promise<void>((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    xhr.open('PUT', `/api/files/uploads/${encodeURIComponent(uploadId)}/chunks/${chunkIndex}`)
    xhr.setRequestHeader('Content-Type', 'application/octet-stream')

    if (options.signal) {
      options.signal.addEventListener('abort', () => xhr.abort(), { once: true })
    }

    xhr.upload.addEventListener('progress', (event) => {
      options.onProgress?.({
        loaded: event.loaded,
        total: event.lengthComputable ? event.total : null,
      })
    })

    xhr.addEventListener('load', () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve()
        return
      }
      reject(withStatus(`Chunk ${chunkIndex + 1} failed: ${problemDetail(xhr)}`, xhr.status))
    })

    xhr.addEventListener('error', () =>
      reject(new Error(`Chunk ${chunkIndex + 1} failed: network error.`)),
    )
    xhr.addEventListener('abort', () => reject(new Error('Upload cancelled.')))
    xhr.send(blob)
  })
}

const CHUNK_RETRIES = 3

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

export async function uploadFileChunked(
  dir: string,
  file: File,
  options: {
    overwrite?: boolean
    chunkSize?: number
    onProgress?: (progress: UploadProgress) => void
    signal?: AbortSignal
  } = {},
): Promise<FileEntry> {
  const chunkSize = options.chunkSize ?? DEFAULT_CHUNK_BYTES
  const plans = chunkPlan(file.size, chunkSize)
  const session = await initUpload(dir, file.name, file.size, plans.length, options.overwrite ?? false)

  const received = new Set(session.receivedChunks)
  let done = plans.filter((plan) => received.has(plan.index)).reduce((sum, plan) => sum + plan.length, 0)
  options.onProgress?.({ loaded: done, total: file.size })

  try {
    for (const plan of plans) {
      if (received.has(plan.index)) continue
      options.signal?.throwIfAborted()
      const blob = file.slice(plan.offset, plan.offset + plan.length)
      let lastError: unknown = null
      for (let attempt = 0; attempt <= CHUNK_RETRIES; attempt += 1) {
        try {
          await putChunk(session.uploadId, plan.index, blob, {
            signal: options.signal,
            onProgress: ({ loaded }) => {
              options.onProgress?.({ loaded: done + loaded, total: file.size })
            },
          })
          lastError = null
          break
        } catch (err) {
          lastError = err
          if (options.signal?.aborted) throw err
          if (attempt < CHUNK_RETRIES) await delay(1000 * 2 ** attempt)
        }
      }
      if (lastError) throw lastError
      received.add(plan.index)
      done += plan.length
      options.onProgress?.({ loaded: done, total: file.size })
    }

    return await completeUpload(session.uploadId)
  } catch (err) {
    // No cross-reload resume UI yet, so don't orphan a partial session.
    try {
      await abortUpload(session.uploadId)
    } catch {
      // best effort; stale sessions are swept server-side after the TTL
    }
    throw err
  }
}

export function uploadFiles(
  dir: string,
  files: FileList | File[],
  options: {
    overwrite?: boolean
    onProgress?: (progress: UploadProgress) => void
    signal?: AbortSignal
  } = {},
): Promise<FileEntry[]> {
  const form = new FormData()
  const list = Array.from(files)
  for (const file of list) {
    form.append('files', file, file.name)
  }

  const params = new URLSearchParams()
  params.set('path', dir)
  if (options.overwrite) params.set('overwrite', 'true')

  return new Promise<FileEntry[]>((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    xhr.open('POST', `/api/files/upload?${params.toString()}`)

    if (options.signal) {
      options.signal.addEventListener('abort', () => xhr.abort(), { once: true })
    }

    xhr.upload.addEventListener('progress', (event) => {
      options.onProgress?.({
        loaded: event.loaded,
        total: event.lengthComputable ? event.total : null,
      })
    })

    xhr.addEventListener('load', () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          resolve(JSON.parse(xhr.responseText) as FileEntry[])
        } catch {
          reject(new Error('Upload succeeded but the response was not valid JSON.'))
        }
        return
      }

      let detail = xhr.statusText
      try {
        const problem = JSON.parse(xhr.responseText) as { title?: string; detail?: string }
        detail = problem.detail ?? problem.title ?? detail
      } catch {
        // keep status text
      }
      const error = new Error(detail) as Error & { status?: number }
      error.status = xhr.status
      reject(error)
    })

    xhr.addEventListener('error', () => reject(new Error('Upload failed: network error.')))
    xhr.addEventListener('abort', () => reject(new Error('Upload cancelled.')))
    xhr.send(form)
  })
}
