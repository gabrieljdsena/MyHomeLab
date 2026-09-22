import { get, post } from './client'
import type { DockerActionResult, DockerContainer } from '../lib/types'

export const listDockerContainers = () => get<DockerContainer[]>('/docker/containers')

export const getDockerContainer = (name: string) => get<DockerContainer>(`/docker/containers/${encodeURIComponent(name)}`)

export const getAppDockerContainer = (appId: string) => get<DockerContainer>(`/apps/${appId}/docker`)

export const executeAppDockerAction = (appId: string, action: 'start' | 'stop' | 'restart') =>
  post<DockerActionResult>(`/apps/${appId}/docker/${action}`)

export const executeDockerAction = (name: string, action: 'start' | 'stop' | 'restart') =>
  post<DockerActionResult>(`/docker/containers/${encodeURIComponent(name)}/${action}`)
