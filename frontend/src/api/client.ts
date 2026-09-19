import type { ApiError } from '../lib/types'

const BASE = '/api'

export class HttpError extends Error implements ApiError {
  status: number
  title: string
  detail?: string

  constructor(status: number, title: string, detail?: string) {
    super(detail ?? title)
    this.name = 'HttpError'
    this.status = status
    this.title = title
    this.detail = detail
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(init?.headers ?? {}),
    },
    ...init,
  })

  if (!response.ok) {
    let problem: Partial<ApiError> = {}
    try {
      problem = await response.json()
    } catch {
      // non-JSON error body; fall back to status text
    }
    throw new HttpError(
      response.status,
      problem.title ?? response.statusText,
      problem.detail,
    )
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const get = <T>(path: string, init?: RequestInit) => request<T>(path, init)

export const post = <T>(path: string, body?: unknown) =>
  request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) })

export const put = <T>(path: string, body: unknown) =>
  request<T>(path, { method: 'PUT', body: JSON.stringify(body) })

export const patch = <T>(path: string, body: unknown) =>
  request<T>(path, { method: 'PATCH', body: JSON.stringify(body) })

export const del = <T>(path: string) => request<T>(path, { method: 'DELETE' })