import type { ReactNode } from 'react'

export function Icon({
  name,
  className = '',
  filled = false,
}: {
  name: string
  className?: string
  filled?: boolean
}) {
  return (
    <span
      aria-hidden="true"
      className={`material-symbols-outlined select-none ${className}`}
      style={filled ? { fontVariationSettings: '"FILL" 1, "wght" 500, "GRAD" 0, "opsz" 24' } : undefined}
    >
      {name}
    </span>
  )
}

export function Spinner() {
  const dots: ReactNode[] = [0, 1, 2].map((i) => (
    <span key={i} className="size-1.5 rounded-full bg-accent" />
  ))
  return (
    <span className="flex animate-pulse items-center gap-1" role="status" aria-label="loading">
      {dots}
    </span>
  )
}