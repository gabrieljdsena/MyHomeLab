import { del, get, patch, post, put, HttpError } from './client'
import type {
  AppDetail,
  CreateAppInput,
  DeployStatus,
  HealthHistory,
  HealthResult,
  PatchAppInput,
  UpdateAppInput,
} from '../lib/types'

export type AppQuery = {
  search?: string
  category?: string
  enabledOnly?: boolean
}

function queryString(query: AppQuery): string {
  const params = new URLSearchParams()
  if (query.search) params.set('search', query.search)
  if (query.category) params.set('category', query.category)
  if (query.enabledOnly !== undefined) params.set('enabledOnly', String(query.enabledOnly))
  const value = params.toString()
  return value ? `?${value}` : ''
}

export const listApps = (query: AppQuery = {}) =>
  get<AppDetail[]>(`/apps${queryString(query)}`)

export const getApp = (id: string) => get<AppDetail>(`/apps/${id}`)

export const createApp = (input: CreateAppInput) =>
  post<AppDetail>('/apps', input)

export const updateApp = (id: string, input: UpdateAppInput) =>
  put<AppDetail>(`/apps/${id}`, input)

export const patchApp = (id: string, changes: PatchAppInput) =>
  patch<AppDetail>(`/apps/${id}`, changes)

export const deleteApp = (id: string) => del<void>(`/apps/${id}`)

export const probeApp = (id: string) => post<HealthResult>(`/apps/${id}/check`)

export const deployApp = (id: string) => post<DeployStatus>(`/apps/${id}/deploy`)

export const getDeployStatus = async (id: string): Promise<DeployStatus | null> => {
  try {
    return await get<DeployStatus>(`/apps/${id}/deploy`)
  } catch (err) {
    if (err instanceof HttpError && err.status === 404) return null
    throw err
  }
}

export const getHealthHistory = (id: string, hours = 24, limit = 200) =>
  get<HealthHistory>(`/apps/${id}/history?hours=${hours}&limit=${limit}`)

export const listCategories = () => get<string[]>(`/categories`)