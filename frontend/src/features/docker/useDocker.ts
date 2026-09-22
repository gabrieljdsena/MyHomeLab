import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { executeAppDockerAction, getAppDockerContainer, listDockerContainers } from '../../api/docker'
import { queryKeys } from '../../lib/queryKeys'

export function useDockerContainers() {
  return useQuery({
    queryKey: queryKeys.dockerContainers,
    queryFn: listDockerContainers,
    staleTime: 10_000,
    refetchInterval: 15_000,
  })
}

export function useAppDocker(appId: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.appDocker(appId),
    queryFn: () => getAppDockerContainer(appId),
    enabled: Boolean(appId) && enabled,
    retry: false,
    staleTime: 5_000,
    refetchInterval: 15_000,
  })
}

export function useAppDockerAction() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ appId, action }: { appId: string; action: 'start' | 'stop' | 'restart' }) =>
      executeAppDockerAction(appId, action),
    onSuccess: (_data, variables) => {
      client.invalidateQueries({ queryKey: queryKeys.appDocker(variables.appId) })
      client.invalidateQueries({ queryKey: queryKeys.dockerContainers })
      client.invalidateQueries({ queryKey: queryKeys.apps })
    },
  })
}
