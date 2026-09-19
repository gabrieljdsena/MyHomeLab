import { useMutation, useQuery } from '@tanstack/react-query'
import { executeTerminal, getTerminalConfig, type TerminalExecuteRequest } from '../../api/terminal'
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
