import { get } from './client'
import type { LogEntry } from '../lib/types'

export type LogQuery = {
  search?: string
  application?: string
  limit?: number
}

function queryString(query: LogQuery): string {
  const params = new URLSearchParams()
  if (query.search) params.set('search', query.search)
  if (query.application) params.set('application', query.application)
  if (query.limit !== undefined) params.set('limit', String(query.limit))
  const value = params.toString()
  return value ? `?${value}` : ''
}

export const listLogs = (query: LogQuery = {}) => get<LogEntry[]>(`/logs${queryString(query)}`)

export const getLog = (id: number) => get<LogEntry>(`/logs/${id}`)
