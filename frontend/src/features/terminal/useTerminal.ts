import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  executeTerminal,
  getTerminalConfig,
  killPtySession,
  listPtySessions,
  type TerminalExecuteRequest,
} from '../../api/terminal'
import { queryKeys } from '../../lib/queryKeys'

export function useTerminalConfig() {
  return useQuery({
    queryKey: queryKeys.terminalConfig,
    queryFn: getTerminalConfig,
    staleTime: 60_000,
  })
}

export function useTerminalExec() {
  return useMutation({
    mutationFn: (request: TerminalExecuteRequest) => executeTerminal(request),
  })
}

export function useTerminalSessions(enabled: boolean) {
  return useQuery({
    queryKey: queryKeys.terminalSessions,
    queryFn: listPtySessions,
    enabled,
    refetchInterval: 10_000,
    staleTime: 5_000,
  })
}

export function useKillPtySession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => killPtySession(id),
    onSettled: () => queryClient.invalidateQueries({ queryKey: queryKeys.terminalSessions }),
  })
}
