import { useQuery } from '@tanstack/react-query'
import { getHealthHistory } from '../../api/apps'
import { queryKeys } from '../../lib/queryKeys'

const REFETCH_MS = 30_000

export function useHealthHistory(id: string, hours = 24, limit = 200, enabled = true) {
  return useQuery({
    queryKey: queryKeys.appHistory(id, hours, limit),
    queryFn: () => getHealthHistory(id, hours, limit),
    enabled: enabled && Boolean(id),
    refetchInterval: REFETCH_MS,
    staleTime: 10_000,
  })
}
