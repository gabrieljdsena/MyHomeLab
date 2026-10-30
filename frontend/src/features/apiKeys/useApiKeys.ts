import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createApiKey,
  listApiKeys,
  revokeApiKey,
  setApiKeyCanWrite,
} from '../../api/apiKeys'
import { queryKeys } from '../../lib/queryKeys'

export function useApiKeys() {
  return useQuery({
    queryKey: queryKeys.apiKeys,
    queryFn: listApiKeys,
    staleTime: 10_000,
  })
}

export function useCreateApiKey() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ name, canWrite }: { name: string; canWrite: boolean }) =>
      createApiKey(name, canWrite),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.apiKeys }),
  })
}

export function useSetApiKeyCanWrite() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, canWrite }: { id: string; canWrite: boolean }) =>
      setApiKeyCanWrite(id, canWrite),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.apiKeys }),
  })
}

export function useRevokeApiKey() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => revokeApiKey(id),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.apiKeys }),
  })
}
