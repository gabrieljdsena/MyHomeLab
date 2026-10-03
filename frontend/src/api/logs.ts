import { get } from './client'
import type { LogEntry, LogPage } from '../lib/types'

export type LogQuery = {
  search?: string
  application?: string
  page?: number
  pageSize?: number
}

function queryString(query: LogQuery): string {
  const params = new URLSearchParams()
  if (query.search) params.set('search', query.search)
  if (query.application) params.set('application', query.application)
  if (query.page !== undefined) params.set('page', String(query.page))
  if (query.pageSize !== undefined) params.set('pageSize', String(query.pageSize))
  const value = params.toString()
  return value ? `?${value}` : ''
}

export const listLogs = (query: LogQuery = {}) => get<LogPage>(`/logs${queryString(query)}`)

export const listLogApplications = () => get<string[]>(`/logs/applications`)

export const getLog = (id: number) => get<LogEntry>(`/logs/${id}`)
