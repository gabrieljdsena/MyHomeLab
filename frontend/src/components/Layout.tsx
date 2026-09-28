import { useEffect, useRef, useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { Icon } from './Icon'
import { ConfirmDialog } from './ConfirmDialog'
import { powerOff, reboot } from '../api/system'

const navClass = ({ isActive }: { isActive: boolean }) =>
  `inline-flex cursor-pointer items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition ${
    isActive ? 'bg-accent-soft text-accent' : 'text-muted hover:bg-surface-2 hover:text-text'
  }`

const navClassMobile = ({ isActive }: { isActive: boolean }) =>
  `flex w-full items-center gap-2.5 rounded-lg px-3 py-2.5 text-sm font-medium transition ${
    isActive ? 'bg-accent-soft text-accent' : 'text-muted hover:bg-surface-2 hover:text-text'
  }`

export function Layout() {
  const [mobileOpen, setMobileOpen] = useState(false)
  const [powerOpen, setPowerOpen] = useState(false)
  const [confirmAction, setConfirmAction] = useState<'shutdown' | 'reboot' | null>(null)
  const powerRef = useRef<HTMLDivElement>(null)
  const mobilePanelRef = useRef<HTMLDivElement>(null)
  const mobileButtonRef = useRef<HTMLDivElement>(null)

  const powerMutation = useMutation({
    mutationFn: (action: 'shutdown' | 'reboot') => (action === 'shutdown' ? powerOff() : reboot()),
    onSuccess: () => setConfirmAction(null),
  })

  useEffect(() => {
    if (!mobileOpen && !powerOpen) return
    const onDown = (e: MouseEvent) => {
      const target = e.target as Node
      if (powerOpen && powerRef.current && !powerRef.current.contains(target)) setPowerOpen(false)
      if (
        mobileOpen &&
        mobilePanelRef.current &&
        !mobilePanelRef.current.contains(target) &&
        mobileButtonRef.current &&
        !mobileButtonRef.current.contains(target)
      )
        setMobileOpen(false)
    }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [mobileOpen, powerOpen])

  return (
    <div className="flex min-h-full flex-col">
      <header className="sticky top-0 z-20 border-b border-border/70 bg-background/85 backdrop-blur">
        <div className="mx-auto flex h-14 w-full max-w-7xl items-center justify-between gap-3 px-4 md:h-16 md:px-5">
          <div className="flex min-w-0 items-center gap-2.5">
            <span className="flex size-8 shrink-0 items-center justify-center rounded-xl bg-accent text-white shadow-lg shadow-accent/30 md:size-9">
              <Icon name="dns" className="text-[18px] md:text-[20px]" filled />
            </span>
            <div className="min-w-0 leading-tight">
              <span className="block truncate text-[14px] font-semibold tracking-tight text-text md:text-[15px]">
                MyHomeLab
              </span>
              <p className="hidden truncate text-[11px] text-muted sm:block">Every service, one place</p>
            </div>
          </div>

          <nav className="hidden items-center gap-1 md:flex">
            <NavLink to="/" end className={navClass}>
              <Icon name="dashboard" className="text-[18px]" />
              Dashboard
            </NavLink>
            <NavLink to="/manage" className={navClass}>
              <Icon name="tune" className="text-[18px]" />
              Manage
            </NavLink>
            <NavLink to="/machines" className={navClass}>
              <Icon name="devices" className="text-[18px]" />
              Machines
            </NavLink>
            <NavLink to="/logs" className={navClass}>
              <Icon name="article" className="text-[18px]" />
              Logs
            </NavLink>
            <NavLink to="/postgres" className={navClass}>
              <Icon name="database" className="text-[18px]" />
              Postgres
            </NavLink>
            <NavLink to="/terminal" className={navClass}>
              <Icon name="terminal" className="text-[18px]" />
              Terminal
            </NavLink>
            <span className="mx-1 h-6 w-px bg-border/60" aria-hidden="true" />
            <div className="relative" ref={powerRef}>
              <button
                type="button"
                onClick={() => setPowerOpen((v) => !v)}
                aria-haspopup="menu"
                aria-expanded={powerOpen}
                aria-label="Power options"
                className="inline-flex cursor-pointer items-center gap-1.5 rounded-lg border border-down/30 bg-down/10 px-3 py-2 text-sm font-medium text-down transition hover:bg-down/20 hover:text-down"
              >
                <Icon name="power_settings_new" className="text-[18px]" />
                Power
                <Icon name="expand_more" className={`text-[16px] transition ${powerOpen ? 'rotate-180' : ''}`} />
              </button>
              {powerOpen && (
                <div
                  role="menu"
                  className="absolute right-0 z-30 mt-2 w-44 overflow-hidden rounded-xl border border-border bg-surface shadow-xl shadow-black/30"
                >
                  <button
                    type="button"
                    role="menuitem"
                    onClick={() => {
                      setConfirmAction('shutdown')
                      setPowerOpen(false)
                    }}
                    className="flex w-full cursor-pointer items-center gap-2 px-3 py-2.5 text-left text-sm text-text transition hover:bg-down/10 hover:text-down"
                  >
                    <Icon name="power_settings_new" className="text-[18px] text-down" />
                    Shut down
                  </button>
                  <button
                    type="button"
                    role="menuitem"
                    onClick={() => {
                      setConfirmAction('reboot')
                      setPowerOpen(false)
                    }}
                    className="flex w-full cursor-pointer items-center gap-2 px-3 py-2.5 text-left text-sm text-text transition hover:bg-accent-soft hover:text-accent"
                  >
                    <Icon name="restart_alt" className="text-[18px] text-accent" />
                    Restart
                  </button>
                </div>
              )}
            </div>
          </nav>

          <div ref={mobileButtonRef} className="flex items-center md:hidden">
            <button
              type="button"
              onClick={() => setMobileOpen((v) => !v)}
              aria-label={mobileOpen ? 'Close navigation menu' : 'Open navigation menu'}
              aria-expanded={mobileOpen}
              aria-controls="mobile-nav"
              className="inline-flex size-9 cursor-pointer items-center justify-center rounded-xl border border-border bg-surface text-text transition hover:bg-surface-2"
            >
              <Icon name={mobileOpen ? 'close' : 'menu'} className="text-[22px]" />
            </button>
          </div>
        </div>

        {mobileOpen && (
          <div
            id="mobile-nav"
            ref={mobilePanelRef}
            className="border-t border-border/70 bg-surface/95 backdrop-blur md:hidden"
          >
            <nav className="mx-auto flex max-w-7xl flex-col gap-1 px-4 py-3">
              <NavLink to="/" end className={navClassMobile} onClick={() => setMobileOpen(false)}>
                <Icon name="dashboard" className="text-[18px]" />
                Dashboard
              </NavLink>
              <NavLink to="/manage" className={navClassMobile} onClick={() => setMobileOpen(false)}>
                <Icon name="tune" className="text-[18px]" />
                Manage
              </NavLink>
              <NavLink to="/machines" className={navClassMobile} onClick={() => setMobileOpen(false)}>
                <Icon name="devices" className="text-[18px]" />
                Machines
              </NavLink>
              <NavLink to="/logs" className={navClassMobile} onClick={() => setMobileOpen(false)}>
                <Icon name="article" className="text-[18px]" />
                Logs
              </NavLink>
              <NavLink to="/postgres" className={navClassMobile} onClick={() => setMobileOpen(false)}>
                <Icon name="database" className="text-[18px]" />
                Postgres
              </NavLink>
              <NavLink to="/terminal" className={navClassMobile} onClick={() => setMobileOpen(false)}>
                <Icon name="terminal" className="text-[18px]" />
                Terminal
              </NavLink>

              <div className="my-2 h-px bg-border/60" aria-hidden="true" />

              <p className="px-3 pb-1 text-[11px] font-semibold uppercase tracking-widest text-muted">Power</p>
              <button
                type="button"
                onClick={() => {
                  setConfirmAction('shutdown')
                  setMobileOpen(false)
                }}
                className="flex w-full cursor-pointer items-center gap-2.5 rounded-lg px-3 py-2.5 text-left text-sm font-medium text-text transition hover:bg-down/10 hover:text-down"
              >
                <Icon name="power_settings_new" className="text-[18px] text-down" />
                Shut down
              </button>
              <button
                type="button"
                onClick={() => {
                  setConfirmAction('reboot')
                  setMobileOpen(false)
                }}
                className="flex w-full cursor-pointer items-center gap-2.5 rounded-lg px-3 py-2.5 text-left text-sm font-medium text-text transition hover:bg-accent-soft hover:text-accent"
              >
                <Icon name="restart_alt" className="text-[18px] text-accent" />
                Restart
              </button>
            </nav>
          </div>
        )}
      </header>

      <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6 md:px-5 md:py-8">
        <Outlet />
      </main>

      <footer className="border-t border-border/60 py-4 text-center text-xs text-muted/70">
        MyHomeLab · self-hosted hub
      </footer>

      <ConfirmDialog
        open={confirmAction !== null}
        title={confirmAction === 'reboot' ? 'Restart host?' : 'Shut down host?'}
        message={
          confirmAction === 'reboot'
            ? 'This will reboot the homelab machine (shutdown /r /t 0 on Windows, shutdown -r now on Linux). Services will be briefly offline.'
            : 'This will shut down the homelab machine (shutdown /s /t 0 on Windows, shutdown -h now on Linux). The hub and all services will go offline until the machine is powered on again (WoL or power button).'
        }
        confirmLabel={
          powerMutation.isPending
            ? confirmAction === 'reboot'
              ? 'Restarting…'
              : 'Shutting down…'
            : confirmAction === 'reboot'
              ? 'Restart'
              : 'Shut down'
        }
        busy={powerMutation.isPending}
        onConfirm={() => {
          if (confirmAction) powerMutation.mutate(confirmAction)
        }}
        onCancel={() => {
          if (!powerMutation.isPending) setConfirmAction(null)
        }}
      />
    </div>
  )
}