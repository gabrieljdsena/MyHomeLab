import { get, post } from './client'

export interface AuthUser {
  id: string
  username: string
}

export interface AuthStatus {
  needsSetup: boolean
}

export const getAuthStatus = () => get<AuthStatus>('/auth/status')

export const getMe = () => get<AuthUser>('/auth/me')

export const login = (username: string, password: string) =>
  post<AuthUser>('/auth/login', { username, password })

export const setup = (username: string, password: string) =>
  post<AuthUser>('/auth/setup', { username, password })

export const logout = () => post<void>('/auth/logout')

export const logoutAll = () => post<void>('/auth/logout-all')
