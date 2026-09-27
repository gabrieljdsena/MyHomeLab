import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createMachine,
  deleteMachine,
  getMachine,
  listMachines,
  patchMachine,
  updateMachine,
  type MachineQuery,
} from '../../api/machines'
import { queryKeys } from '../../lib/queryKeys'
import type {
  CreateMachineInput,
  PatchMachineInput,
  UpdateMachineInput,
} from '../../lib/types'

const REFETCH_MS = 15_000

export function useMachines(query: MachineQuery = {}) {
  return useQuery({
    queryKey: [...queryKeys.machines, query],
    queryFn: () => listMachines(query),
    refetchInterval: REFETCH_MS,
  })
}

export function useMachine(id: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.machine(id),
    queryFn: () => getMachine(id),
    enabled: Boolean(id) && enabled,
  })
}

export function useCreateMachine() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (input: CreateMachineInput) => createMachine(input),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.machines }),
  })
}

export function useUpdateMachine() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: UpdateMachineInput }) => updateMachine(id, input),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.machines }),
  })
}

export function usePatchMachine() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, changes }: { id: string; changes: PatchMachineInput }) => patchMachine(id, changes),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.machines }),
  })
}

export function useDeleteMachine() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteMachine(id),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.machines }),
  })
}
