export const PAGE_SIZE_OPTIONS = [25, 50, 100] as const

export type PageItem = number | '…'

export function pageItems(page: number, totalPages: number): PageItem[] {
  if (totalPages <= 7) {
    return Array.from({ length: totalPages }, (_, i) => i + 1)
  }
  if (page <= 4) {
    return [1, 2, 3, 4, 5, '…', totalPages]
  }
  if (page >= totalPages - 3) {
    return [1, '…', totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages]
  }
  return [1, '…', page - 1, page, page + 1, '…', totalPages]
}
