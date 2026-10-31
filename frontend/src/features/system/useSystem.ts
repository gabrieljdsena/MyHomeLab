import { useMutation, useQuery } from '@tanstack/react-query'
import { getPostgresMetrics, getPostgresSchema, getSystemMetrics, runSql } from '../../api/system'
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

export function usePostgresSchema() {
  return useQuery({
    queryKey: queryKeys.systemPostgresSchema,
    queryFn: getPostgresSchema,
    staleTime: 5 * 60_000,
  })
}

export function useRunSql() {
  return useMutation({
    mutationFn: ({ sql, database }: { sql: string; database: string | null }) => runSql(sql, database),
  })
}