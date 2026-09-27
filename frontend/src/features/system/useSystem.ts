import { useQuery } from '@tanstack/react-query'
import { getPostgresMetrics, getSystemMetrics } from '../../api/system'
import { queryKeys } from '../../lib/queryKeys'

const REFETCH_MS = 5_000

export function useSystemMetrics() {
  return useQuery({
    queryKey: queryKeys.system,
    queryFn: getSystemMetrics,
    refetchInterval: REFETCH_MS,
  })
}

export function usePostgresMetrics() {
  return useQuery({
    queryKey: queryKeys.systemPostgres,
    queryFn: getPostgresMetrics,
    refetchInterval: REFETCH_MS,
  })
}