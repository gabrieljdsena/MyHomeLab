import { get, post } from './client'

export interface TerminalExecuteRequest {
  command: string
  cwd?: string | null
  shell?: string | null
}

export interface TerminalExecuteResponse {
  command: string
  shell: string
  cwd: string
  resolvedCwd: string
  output: string
  exitCode: number
  durationMs: number
  timedOut: boolean
  executedAtUtc: string
}

export interface TerminalConfig {
  enabled: boolean
  defaultShell: string
  allowedShells: string[]
  timeoutSeconds: number
  maxOutputBytes: number
  defaultWorkingDirectory: string
}

export const getTerminalConfig = () => get<TerminalConfig>('/terminal/config')

export const executeTerminal = (request: TerminalExecuteRequest) =>
  post<TerminalExecuteResponse>('/terminal/exec', request)
