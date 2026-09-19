export const queryKeys = {
  apps: ['apps'] as const,
  app: (id: string) => ['apps', id] as const,
  categories: ['categories'] as const,
  system: ['system'] as const,
  terminalConfig: ['terminal', 'config'] as const,
}
