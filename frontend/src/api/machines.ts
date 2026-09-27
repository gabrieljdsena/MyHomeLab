import { del, get, patch, post, put } from './client'
import type {
  CreateMachineInput,
  MachineDetail,
  MachineSummary,
  PatchMachineInput,
  UpdateMachineInput,
} from '../lib/types'

export type MachineQuery = {
  search?: string
  enabledOnly?: boolean
}

function queryString(query: MachineQuery): string {
  const params = new URLSearchParams()
  if (query.search) params.set('search', query.search)
  if (query.enabledOnly !== undefined) params.set('enabledOnly', String(query.enabledOnly))
  const value = params.toString()
  return value ? `?${value}` : ''
}

export const listMachines = (query: MachineQuery = {}) =>
  get<MachineSummary[]>(`/machines${queryString(query)}`)

export const getMachine = (id: string) => get<MachineDetail>(`/machines/${id}`)

export const createMachine = (input: CreateMachineInput) =>
  post<MachineDetail>('/machines', input)

export const updateMachine = (id: string, input: UpdateMachineInput) =>
  put<MachineDetail>(`/machines/${id}`, input)

export const patchMachine = (id: string, changes: PatchMachineInput) =>
  patch<MachineDetail>(`/machines/${id}`, changes)

export const deleteMachine = (id: string) => del<void>(`/machines/${id}`)
