export const queryKeys = {
  apps: ['apps'] as const,
  app: (id: string) => ['apps', id] as const,
  categories: ['categories'] as const,
  system: ['system'] as const,
  systemNetwork: ['system', 'network'] as const,
  terminalConfig: ['terminal', 'config'] as const,
  appHistory: (id: string, hours: number, limit: number) => ['apps', id, 'history', hours, limit] as const,
  dockerContainers: ['docker', 'containers'] as const,
  dockerContainer: (name: string) => ['docker', 'containers', name] as const,
  appDocker: (id: string) => ['apps', id, 'docker'] as const,
}
