import { get, post } from './client'
import type { NetworkSample, PostgresMetrics, SystemMetrics } from '../lib/types'

export const getSystemMetrics = () => get<SystemMetrics>('/system')

export const getNetworkTraffic = () => get<NetworkSample[]>('/system/network')

export const getPostgresMetrics = () => get<PostgresMetrics>('/system/postgres')

export interface PowerResponse {
  action: string
  accepted: boolean
  requestedAtUtc: string
  detail?: string | null
}

export const powerOff = () => post<PowerResponse>('/system/power', { action: 'shutdown' })

export const reboot = () => post<PowerResponse>('/system/power', { action: 'reboot' })