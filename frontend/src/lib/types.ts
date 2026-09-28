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
  dockerContainer: string | null
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
  dockerContainer: string | null
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
  dockerContainer?: string | null
  healthCheckEnabled?: boolean
  isEnabled?: boolean
  healthCheckIntervalMs?: number
  sortOrder?: number
}

export interface DockerContainer {
  id: string
  name: string
  image: string
  state: string
  status: string
  ports: string | null
  createdAt: string
}

export interface DockerActionResult {
  container: string
  action: string
  success: boolean
  status: string | null
  state: string | null
  message: string | null
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

export interface NetworkSample {
  sampledAtUtc: string
  downloadBytesPerSec: number
  uploadBytesPerSec: number
}

export interface PostgresMetrics {
  version: string
  databaseName: string
  uptimeSeconds: number
  sizeBytes: number
  connectionsUsed: number
  connectionsMax: number
  connectionsActive: number
  connectionsIdle: number
  lockWaiters: number
  longestQuerySeconds: number
  transactionsPerSecond: number
  transactionsTotal: number
  rollbacksTotal: number
  deadlocks: number
  cacheHitRatio: number
  tempBytesPerSecond: number
  walBytesPerSecond: number
  checkpointsTotal: number
  sampledAtUtc: string
}

export interface AppHealthPoint {
  status: HealthStatus
  latencyMs: number | null
  checkedAtUtc: string
}

export interface HealthUptime {
  uptimePercent: number
  totalChecks: number
  upCount: number
  downCount: number
  averageLatencyMs: number | null
  minLatencyMs: number | null
  maxLatencyMs: number | null
}

export interface HealthHistory {
  points: AppHealthPoint[]
  uptime: HealthUptime
}

export type MachineReachability = 'unknown' | 'online' | 'offline'

export interface MachineSummary {
  id: string
  name: string
  description: string
  hostname: string
  icon: string
  reachability: MachineReachability
  lastLatencyMs: number | null
  ipAddress: string | null
  macAddress: string | null
  isEnabled: boolean
  sortOrder: number
}

export interface MachineDetail extends MachineSummary {
  lastSeenUtc: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateMachineInput {
  name: string
  description: string
  hostname: string
  icon: string
  sortOrder: number
}

export type UpdateMachineInput = CreateMachineInput

export interface PatchMachineInput {
  name?: string
  description?: string
  hostname?: string
  icon?: string
  isEnabled?: boolean
  sortOrder?: number
}

export interface LogEntry {
  id: number
  application: string
  log: string
}
