import { useState } from 'react'
import { useDeleteMachine, useMachines, usePatchMachine } from '../features/machines/useMachines'
import type { MachineSummary } from '../lib/types'
import { MachineCard } from '../components/MachineCard'
import { MachineForm } from '../components/MachineForm'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { Icon, Spinner } from '../components/Icon'

export function Machines() {
  const { data: machines, isLoading, isError, refetch } = useMachines()
  const deleteMutation = useDeleteMachine()
  const patchMutation = usePatchMachine()

  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<MachineSummary | null>(null)
  const [deleting, setDeleting] = useState<MachineSummary | null>(null)

  const openCreate = () => {
    setEditing(null)
    setFormOpen(true)
  }

  const openEdit = (machine: MachineSummary) => {
    setEditing(machine)
    setFormOpen(true)
  }

  const confirmDelete = () => {
    if (!deleting) return
    deleteMutation.mutate(deleting.id, {
      onSuccess: () => setDeleting(null),
    })
  }

  const toggleEnabled = (machine: MachineSummary) =>
    patchMutation.mutate({ id: machine.id, changes: { isEnabled: !machine.isEnabled } })

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    )
  }

  if (isError) {
    return (
      <div className="mx-auto max-w-md rounded-2xl border border-down/40 bg-down/10 p-8 text-center">
        <p className="text-sm text-text">Failed to load machines.</p>
        <button
          type="button"
          onClick={() => refetch()}
          className="mt-4 cursor-pointer rounded-lg border border-border px-4 py-2 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
        >
          Retry
        </button>
      </div>
    )
  }

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold tracking-tight">Machines</h1>
          <p className="mt-1 text-sm text-muted">
            {(machines?.length ?? 0) + (machines?.length === 1 ? ' PC on the LAN' : ' PCs on the LAN')}
          </p>
        </div>
        <button
          type="button"
          onClick={openCreate}
          className="flex cursor-pointer items-center gap-2 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-hover"
        >
          <Icon name="add" filled />
          Add machine
        </button>
      </div>

      {machines && machines.length > 0 ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {machines.map((machine) => (
            <MachineCard
              key={machine.id}
              machine={machine}
              onEdit={() => openEdit(machine)}
              onDelete={() => setDeleting(machine)}
              onToggle={() => toggleEnabled(machine)}
            />
          ))}
        </div>
      ) : (
        <div className="rounded-2xl border border-dashed border-border bg-surface/50 p-12 text-center">
          <Icon name="devices_other" className="text-4xl text-muted/40" />
          <p className="mt-3 text-sm font-medium text-text">No machines registered</p>
          <p className="mx-auto mt-1 max-w-sm text-xs leading-relaxed text-muted">
            Add a LAN PC by its hostname to get reachability, shutdown and restart controls.
          </p>
          <button
            type="button"
            onClick={openCreate}
            className="mt-4 cursor-pointer rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-hover"
          >
            Add machine
          </button>
        </div>
      )}

      {formOpen && <MachineForm machine={editing} onClose={() => setFormOpen(false)} />}

      <ConfirmDialog
        open={deleting !== null}
        title={deleting ? `Delete "${deleting.name}"?` : ''}
        message="This only removes the machine from MyHomeLab. The PC itself is untouched."
        confirmLabel="Delete"
        busy={deleteMutation.isPending}
        onConfirm={confirmDelete}
        onCancel={() => setDeleting(null)}
      />

      {patchMutation.isError && (
        <p className="mt-4 rounded-lg border border-down/40 bg-down/10 px-3 py-2 text-sm text-down">
          {(patchMutation.error as Error)?.message ?? 'Update failed.'}
        </p>
      )}
    </div>
  )
}
