import { get, post } from './client'
import type { SystemMetrics } from '../lib/types'

export const getSystemMetrics = () => get<SystemMetrics>('/system')

export interface PowerResponse {
  action: string
  accepted: boolean
  requestedAtUtc: string
  detail?: string | null
}

export const powerOff = () => post<PowerResponse>('/system/power', { action: 'shutdown' })

export const reboot = () => post<PowerResponse>('/system/power', { action: 'reboot' })