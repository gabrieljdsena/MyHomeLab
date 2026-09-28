import { useQuery } from '@tanstack/react-query'
import { getLog, listLogs, type LogQuery } from '../../api/logs'
import { queryKeys } from '../../lib/queryKeys'

const REFETCH_MS = 15_000

export function useLogs(query: LogQuery = {}) {
  return useQuery({
    queryKey: [...queryKeys.logs, query],
    queryFn: () => listLogs(query),
    refetchInterval: REFETCH_MS,
  })
}

export function useLog(id: number, enabled = true) {
  return useQuery({
    queryKey: queryKeys.log(id),
    queryFn: () => getLog(id),
    enabled: Number.isInteger(id) && enabled,
  })
}
