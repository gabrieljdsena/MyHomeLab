export const DEFAULT_CHUNK_BYTES = 8 * 1024 * 1024

export const SINGLE_UPLOAD_MAX_BYTES = 8 * 1024 * 1024

export interface ChunkPlan {
  index: number
  offset: number
  length: number
}

export function chunkPlan(totalSize: number, chunkSize: number): ChunkPlan[] {
  if (!Number.isInteger(totalSize) || totalSize < 1) {
    throw new Error('Total size must be a positive integer.')
  }
  if (!Number.isInteger(chunkSize) || chunkSize < 1) {
    throw new Error('Chunk size must be a positive integer.')
  }
  const totalChunks = Math.ceil(totalSize / chunkSize)
  const plans: ChunkPlan[] = []
  for (let index = 0; index < totalChunks; index += 1) {
    const offset = index * chunkSize
    plans.push({ index, offset, length: Math.min(chunkSize, totalSize - offset) })
  }
  return plans
}

export function missingChunks(totalChunks: number, received: readonly number[]): number[] {
  const seen = new Set(received)
  const missing: number[] = []
  for (let index = 0; index < totalChunks; index += 1) {
    if (!seen.has(index)) missing.push(index)
  }
  return missing
}
