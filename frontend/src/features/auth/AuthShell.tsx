import type { ReactNode } from 'react'
import { Icon } from '../../components/Icon'

export function AuthShell({ title, subtitle, children }: { title: string; subtitle: string; children: ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center px-4">
      <div className="w-full max-w-sm rounded-2xl border border-border bg-surface p-6 shadow-xl shadow-black/30">
        <div className="flex items-center gap-2.5">
          <span className="flex size-9 items-center justify-center rounded-xl bg-accent text-white shadow-lg shadow-accent/30">
            <Icon name="dns" className="text-[20px]" filled />
          </span>
          <div className="leading-tight">
            <span className="block text-[15px] font-semibold tracking-tight text-text">MyHomeLab</span>
            <p className="text-[11px] text-muted">Every service, one place</p>
          </div>
        </div>
        <h1 className="mt-5 text-lg font-semibold text-text">{title}</h1>
        <p className="mt-1 text-sm text-muted">{subtitle}</p>
        {children}
      </div>
    </div>
  )
}
