import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../../lib/queryKeys'
import {
  createApp,
  deleteApp,
  listApps,
  listCategories,
  patchApp,
  probeApp,
  updateApp,
  type AppQuery,
} from '../../api/apps'
import type { CreateAppInput, PatchAppInput, UpdateAppInput } from '../../lib/types'

const REFETCH_MS = 15_000

export function useApps(query: AppQuery = {}) {
  return useQuery({
    queryKey: [...queryKeys.apps, query],
    queryFn: () => listApps(query),
    refetchInterval: REFETCH_MS,
  })
}

export function useCategories() {
  return useQuery({
    queryKey: queryKeys.categories,
    queryFn: listCategories,
    staleTime: 5 * 60 * 1000,
  })
}

export function useCreateApp() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: CreateAppInput) => createApp(input),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.apps }),
  })
}

export function useUpdateApp() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateAppInput }) => updateApp(id, input),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.apps }),
  })
}

export function usePatchApp() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, changes }: { id: string; changes: PatchAppInput }) => patchApp(id, changes),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.apps }),
  })
}

export function useDeleteApp() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteApp(id),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.apps }),
  })
}

export function useProbeApp() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => probeApp(id),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.apps }),
  })
}