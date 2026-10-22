import { get, post } from './client'
import type { NetworkSample, PostgresMetrics, SystemMetrics } from '../lib/types'

export const getSystemMetrics = () => get<SystemMetrics>('/system')

export const getNetworkTraffic = () => get<NetworkSample[]>('/system/network')

export const getPostgresMetrics = () => get<PostgresMetrics>('/system/postgres')

export interface SqlWorkbenchResult {
  columns: string[]
  rows: (string | number | boolean | null)[][]
  rowCount: number
  command: string
  durationMs: number
  truncated: boolean
}

export const runSql = (sql: string, database?: string | null) =>
  post<SqlWorkbenchResult>('/system/postgres/query', { sql, database: database ?? null })

export interface PostgresRelation {
  schema: string
  name: string
  kind: 'table' | 'view' | 'materialized_view'
}

export interface PostgresDatabase {
  name: string
  relations: PostgresRelation[]
}

export interface PostgresCatalog {
  current: string
  databases: PostgresDatabase[]
}

export const getPostgresSchema = () => get<PostgresCatalog>('/system/postgres/schema')

export interface PowerResponse {
  action: string
  accepted: boolean
  requestedAtUtc: string
  detail?: string | null
}

export const powerOff = () => post<PowerResponse>('/system/power', { action: 'shutdown' })

export const reboot = () => post<PowerResponse>('/system/power', { action: 'reboot' })