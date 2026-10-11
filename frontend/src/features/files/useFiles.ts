import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  deletePath,
  getFilesConfig,
  listFiles,
  mkdir,
  renamePath,
} from '../../api/files'
import { queryKeys } from '../../lib/queryKeys'

export function useFilesConfig() {
  return useQuery({
    queryKey: queryKeys.filesConfig,
    queryFn: getFilesConfig,
    staleTime: 60_000,
  })
}

export function useFileList(path: string) {
  return useQuery({
    queryKey: queryKeys.fileList(path),
    queryFn: () => listFiles(path),
  })
}

export function useMkdir(path: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ parentPath, name }: { parentPath: string; name: string }) =>
      mkdir(parentPath, name),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: queryKeys.fileList(path) })
    },
  })
}

export function useRename() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ from, to }: { from: string; to: string }) => renamePath(from, to),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: queryKeys.files })
    },
  })
}

export function useDeletePath(path: string) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ target, recursive }: { target: string; recursive: boolean }) =>
      deletePath(target, recursive),
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: queryKeys.fileList(path) })
    },
  })
}
