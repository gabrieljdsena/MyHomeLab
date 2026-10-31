import { describe, expect, it } from 'vitest'
import { HttpError } from '../../api/client'
import { authErrorMessage } from './authHelpers'

describe('authErrorMessage', () => {
  it('prefers the problem detail', () => {
    expect(authErrorMessage(new HttpError(401, 'Unauthorized', 'Invalid username or password.'))).toBe(
      'Invalid username or password.',
    )
  })

  it('falls back to the problem title without detail', () => {
    expect(authErrorMessage(new HttpError(409, 'Conflict'))).toBe('Conflict')
  })

  it('explains rate limiting', () => {
    expect(authErrorMessage(new HttpError(429, 'Too Many Requests', 'Too many attempts. Try again later.'))).toBe(
      'Too many attempts. Wait a few minutes and try again.',
    )
  })

  it('handles unknown errors', () => {
    expect(authErrorMessage(new Error('boom'))).toBe('Something went wrong. Try again.')
    expect(authErrorMessage(undefined)).toBe('Something went wrong. Try again.')
  })
})
