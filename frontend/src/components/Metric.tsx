function barColor(percent: number): string {
  if (percent >= 90) return 'bg-gradient-to-r from-down/70 to-down'
  if (percent >= 70) return 'bg-gradient-to-r from-warn/70 to-warn'
  return 'bg-gradient-to-r from-accent/70 to-accent'
}

export function Ring({ percent, label }: { percent: number; label: string }) {
  const size = 96
  const stroke = 9
  const radius = (size - stroke) / 2
  const circumference = 2 * Math.PI * radius
  const clamped = Math.min(100, Math.max(0, percent))
  const offset = circumference - (clamped / 100) * circumference
  const gradientId = `ring-gradient-${label}`

  return (
    <div className="relative size-24" aria-label={`${label} ${Math.round(clamped)}%`}>
      <svg width={size} height={size} className="-rotate-90">
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="var(--color-surface-2)"
          strokeWidth={stroke}
        />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke={`url(#${gradientId})`}
          strokeWidth={stroke}
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          className="transition-[stroke-dashoffset] duration-700 ease-out"
        />
        <defs>
          <linearGradient id={gradientId} x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="var(--color-accent)" />
            <stop offset="100%" stopColor="var(--color-accent-hover)" />
          </linearGradient>
        </defs>
      </svg>
      <div className="absolute inset-0 flex flex-col items-center justify-center">
        <span className="text-xl font-bold tabular-nums tracking-tight">{Math.round(clamped)}%</span>
        <span className="text-[10px] font-medium uppercase tracking-wider text-muted">{label}</span>
      </div>
    </div>
  )
}

export function Bar({ percent, className = '' }: { percent: number; className?: string }) {
  const clamped = Math.min(100, Math.max(0, percent))
  return (
    <div className={`h-1.5 overflow-hidden rounded-full bg-surface-2 ${className}`}>
      <div
        className={`h-full rounded-full transition-[width] duration-700 ease-out ${barColor(percent)}`}
        style={{ width: `${clamped}%` }}
      />
    </div>
  )
}
