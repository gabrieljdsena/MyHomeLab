import { del, get, post } from './client'

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
