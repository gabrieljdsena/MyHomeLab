import { HttpError } from '../../api/client'

export const authInputClass =
  'w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text outline-none placeholder:text-muted/50 focus:border-accent'

export function authErrorMessage(error: unknown): string {
  if (error instanceof HttpError) {
    if (error.status === 429) {
      return 'Too many attempts. Wait a few minutes and try again.'
    }

    return error.detail ?? error.title
  }

  return 'Something went wrong. Try again.'
}
