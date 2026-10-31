import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getAuthStatus, getMe, login, logout, setup, type AuthUser } from '../../api/auth'
import { queryKeys } from '../../lib/queryKeys'

export function useAuthStatus() {
  return useQuery({
    queryKey: queryKeys.authStatus,
    queryFn: getAuthStatus,
    staleTime: 30_000,
    retry: false,
  })
}

export function useMe(enabled = true) {
  return useQuery({
    queryKey: queryKeys.authMe,
    queryFn: getMe,
    enabled,
    staleTime: 5 * 60_000,
    retry: false,
  })
}

interface Credentials {
  username: string
  password: string
}

export function useLogin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ username, password }: Credentials) => login(username, password),
    onSuccess: (user) => {
      queryClient.setQueryData(queryKeys.authMe, user)
    },
  })
}

export function useSetup() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ username, password }: Credentials) => setup(username, password),
    onSuccess: (user) => {
      queryClient.setQueryData(queryKeys.authMe, user)
      queryClient.setQueryData(queryKeys.authStatus, { needsSetup: false })
    },
  })
}

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: logout,
    onSuccess: () => {
      queryClient.clear()
      queryClient.setQueryData<AuthUser | null>(queryKeys.authMe, null)
    },
  })
}
