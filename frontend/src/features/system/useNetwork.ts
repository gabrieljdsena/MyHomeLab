import { useQuery } from '@tanstack/react-query'
import { getNetworkTraffic } from '../../api/system'
import { queryKeys } from '../../lib/queryKeys'

const REFETCH_MS = 5_000

export function useNetworkTraffic() {
  return useQuery({
    queryKey: queryKeys.systemNetwork,
    queryFn: getNetworkTraffic,
    refetchInterval: REFETCH_MS,
  })
}