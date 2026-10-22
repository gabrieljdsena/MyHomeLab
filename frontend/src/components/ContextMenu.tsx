import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Icon } from './Icon'

export interface ContextMenuItem {
  id: string
  label: string
  icon: string
}

export function ContextMenu({
  x,
  y,
  items,
  onPick,
  onClose,
}: {
  x: number
  y: number
  items: ContextMenuItem[]
  onPick: (id: string) => void
  onClose: () => void
}) {
  const ref = useRef<HTMLDivElement>(null)
  const [position, setPosition] = useState({ x, y })

  useLayoutEffect(() => {
    const element = ref.current
    if (!element) return
    const { width, height } = element.getBoundingClientRect()
    setPosition({
      x: Math.max(8, Math.min(x, window.innerWidth - width - 8)),
      y: Math.max(8, Math.min(y, window.innerHeight - height - 8)),
    })
  }, [x, y])

  useEffect(() => {
    ref.current?.querySelector('button')?.focus()

    const onPointerDown = (event: MouseEvent) => {
      if (!ref.current?.contains(event.target as Node)) onClose()
    }
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('mousedown', onPointerDown)
    window.addEventListener('keydown', onKeyDown)
    window.addEventListener('scroll', onClose, true)
    window.addEventListener('resize', onClose)
    return () => {
      window.removeEventListener('mousedown', onPointerDown)
      window.removeEventListener('keydown', onKeyDown)
      window.removeEventListener('scroll', onClose, true)
      window.removeEventListener('resize', onClose)
    }
  }, [onClose])

  const moveFocus = (direction: 1 | -1) => {
    const buttons = Array.from(ref.current?.querySelectorAll('button') ?? [])
    if (buttons.length === 0) return
    const current = buttons.indexOf(document.activeElement as HTMLButtonElement)
    const next = (current + direction + buttons.length) % buttons.length
    buttons[next].focus()
  }

  return createPortal(
    <div
      ref={ref}
      role="menu"
      style={{ left: position.x, top: position.y }}
      onContextMenu={(event) => event.preventDefault()}
      onKeyDown={(event) => {
        if (event.key === 'ArrowDown') {
          event.preventDefault()
          moveFocus(1)
        }
        if (event.key === 'ArrowUp') {
          event.preventDefault()
          moveFocus(-1)
        }
      }}
      className="fixed z-50 min-w-44 rounded-xl border border-border bg-surface-2 py-1 shadow-xl shadow-black/40"
    >
      {items.map((item) => (
        <button
          key={item.id}
          type="button"
          role="menuitem"
          onClick={() => onPick(item.id)}
          className="flex w-full cursor-pointer items-center gap-2 px-2.5 py-1.5 text-left text-[11.5px] text-text outline-none transition hover:bg-surface hover:text-accent focus:bg-surface focus:text-accent"
        >
          <Icon name={item.icon} className="text-[14px] text-muted/70" />
          {item.label}
        </button>
      ))}
    </div>,
    document.body,
  )
}
