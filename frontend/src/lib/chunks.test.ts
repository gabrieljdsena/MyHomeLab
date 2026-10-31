import { describe, expect, it } from 'vitest'
import { chunkPlan, missingChunks } from './chunks'

describe('chunkPlan', () => {
  it('splits evenly divisible sizes', () => {
    expect(chunkPlan(16, 8)).toEqual([
      { index: 0, offset: 0, length: 8 },
      { index: 1, offset: 8, length: 8 },
    ])
  })

  it('makes the last chunk smaller', () => {
    const plans = chunkPlan(20, 8)
    expect(plans).toHaveLength(3)
    expect(plans[2]).toEqual({ index: 2, offset: 16, length: 4 })
    const total = plans.reduce((sum, plan) => sum + plan.length, 0)
    expect(total).toBe(20)
  })

  it('handles a single-chunk file', () => {
    expect(chunkPlan(5, 8)).toEqual([{ index: 0, offset: 0, length: 5 }])
  })

  it('rejects non-positive sizes', () => {
    expect(() => chunkPlan(0, 8)).toThrow()
    expect(() => chunkPlan(-1, 8)).toThrow()
    expect(() => chunkPlan(10, 0)).toThrow()
  })
})

describe('missingChunks', () => {
  it('returns indexes not yet received', () => {
    expect(missingChunks(4, [0, 2])).toEqual([1, 3])
  })

  it('returns empty when complete', () => {
    expect(missingChunks(2, [0, 1])).toEqual([])
  })
})
