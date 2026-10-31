import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys } from '../../lib/queryKeys'
import {
  createApp,
  deleteApp,
  deployApp,
  getDeployStatus,
  listApps,
  listCategories,
  patchApp,
  probeApp,
  updateApp,
  type AppQuery,
} from '../../api/apps'
import type { CreateAppInput, DeployState, PatchAppInput, UpdateAppInput } from '../../lib/types'

const REFETCH_MS = 15_000
const DEPLOY_POLL_MS = 3_000

const ACTIVE_DEPLOY_STATES: DeployState[] = ['queued', 'building', 'restarting']

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
    onSuccess: (_data, id) => {
      client.invalidateQueries({ queryKey: queryKeys.apps })
      client.invalidateQueries({ queryKey: ['apps', id, 'history'] })
    },
  })
}

export function useDeployStatus(appId: string) {
  return useQuery({
    queryKey: queryKeys.appDeploy(appId),
    queryFn: () => getDeployStatus(appId),
    refetchInterval: (query) => {
      const state = query.state.data?.state
      return state && ACTIVE_DEPLOY_STATES.includes(state) ? DEPLOY_POLL_MS : false
    },
    retry: 3,
    retryDelay: 3_000,
  })
}

export function useStartDeploy() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deployApp(id),
    onSuccess: (status) => {
      client.setQueryData(queryKeys.appDeploy(status.appId), status)
      client.invalidateQueries({ queryKey: queryKeys.apps })
    },
  })
}