import { del, get, patch, post } from './client'

export interface ApiKey {
  id: string
  name: string
  prefix: string
  createdAtUtc: string
  lastUsedAtUtc: string | null
  revokedAtUtc: string | null
  canWrite: boolean
}

export interface CreateApiKeyResult {
  apiKey: ApiKey
  token: string
}

export const listApiKeys = () => get<ApiKey[]>('/api-keys')

export const createApiKey = (name: string, canWrite: boolean) =>
  post<CreateApiKeyResult>('/api-keys', { name, canWrite })

export const setApiKeyCanWrite = (id: string, canWrite: boolean) =>
  patch<ApiKey>(`/api-keys/${id}`, { canWrite })

export const revokeApiKey = (id: string) => del<void>(`/api-keys/${id}`)
