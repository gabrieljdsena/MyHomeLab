export type HealthStatus = 'unknown' | 'up' | 'down'

export interface AppDetail {
  id: string
  name: string
  description: string
  url: string
  icon: string
  category: string
  port: number | null
  tags: string[]
  healthCheckEnabled: boolean
  healthCheckIntervalMs: number
  healthStatus: HealthStatus
  lastHealthCheckUtc: string | null
  lastLatencyMs: number | null
  isEnabled: boolean
  sortOrder: number
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateAppInput {
  name: string
  url: string
  description: string
  icon: string
  category: string
  port: number | null
  tags: string[]
  healthCheckEnabled: boolean
  healthCheckIntervalMs: number
  sortOrder: number
}

export type UpdateAppInput = CreateAppInput

export interface PatchAppInput {
  name?: string
  url?: string
  description?: string
  icon?: string
  category?: string
  port?: number
  tags?: string[]
  healthCheckEnabled?: boolean
  isEnabled?: boolean
  healthCheckIntervalMs?: number
  sortOrder?: number
}

export interface HealthResult {
  status: HealthStatus
  latencyMs: number | null
}

export interface ApiError {
  status: number
  title: string
  detail?: string
}

export interface DiskMetric {
  name: string
  driveType: string
  fileSystem: string
  totalBytes: number
  availableBytes: number
  usedBytes: number
  usagePercent: number
}

export interface TemperatureReading {
  component: string
  name: string
  celsius: number
}

export interface StorageHealthReading {
  model: string
  remainingLifePercent: number | null
  dataWrittenBytes: number | null
  powerOnHours: number | null
}

export interface SystemMetrics {
  hostName: string
  operatingSystem: string
  architecture: string
  runtimeVersion: string
  processorCount: number
  cpuUsagePercent: number
  totalMemoryBytes: number
  availableMemoryBytes: number
  usedMemoryBytes: number
  memoryUsagePercent: number
  uptimeSeconds: number
  sampledAtUtc: string
  disks: DiskMetric[]
  temperatures: TemperatureReading[]
  storageHealth: StorageHealthReading[]
}
