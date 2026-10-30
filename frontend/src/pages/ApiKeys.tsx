import { useState, type FormEvent } from 'react'
import { HttpError } from '../api/client'
import type { ApiKey, CreateApiKeyResult } from '../api/apiKeys'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { Icon, Spinner } from '../components/Icon'
import {
  useApiKeys,
  useCreateApiKey,
  useRevokeApiKey,
  useSetApiKeyCanWrite,
} from '../features/apiKeys/useApiKeys'
import { timeAgo } from '../lib/format'

const inputClass =
  'w-full rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm text-text placeholder:text-muted/60 outline-none transition focus:border-accent'

function errorText(error: unknown): string {
  if (error instanceof HttpError) return error.detail ?? error.title
  return 'Something went wrong.'
}

function KeyStatus({ apiKey }: { apiKey: ApiKey }) {
  if (apiKey.revokedAtUtc) {
    return (
      <span className="rounded-full border border-down/30 bg-down/10 px-2.5 py-1 text-[11px] font-semibold uppercase tracking-wide text-down">
        Revoked
      </span>
    )
  }

  return (
    <span className="rounded-full border border-up/30 bg-up/10 px-2.5 py-1 text-[11px] font-semibold uppercase tracking-wide text-up">
      Active
    </span>
  )
}

function PermissionBadge({ apiKey }: { apiKey: ApiKey }) {
  if (apiKey.revokedAtUtc) return null

  return (
    <span
      className={`rounded-full px-2.5 py-1 text-[11px] font-semibold uppercase tracking-wide ${
        apiKey.canWrite
          ? 'border border-warn/30 bg-warn/10 text-warn'
          : 'border border-border bg-surface-2 text-muted'
      }`}
    >
      {apiKey.canWrite ? 'Read + actions' : 'Read-only'}
    </span>
  )
}

export function ApiKeys() {
  const { data: keys, isLoading, isError, refetch } = useApiKeys()
  const createMutation = useCreateApiKey()
  const revokeMutation = useRevokeApiKey()
  const permissionMutation = useSetApiKeyCanWrite()
  const [name, setName] = useState('')
  const [canWrite, setCanWrite] = useState(false)
  const [created, setCreated] = useState<CreateApiKeyResult | null>(null)
  const [copied, setCopied] = useState(false)
  const [revoking, setRevoking] = useState<ApiKey | null>(null)

  const submit = (event: FormEvent) => {
    event.preventDefault()
    const cleanName = name.trim()
    if (!cleanName) return
    createMutation.mutate({ name: cleanName, canWrite }, {
      onSuccess: (result) => {
        setCreated(result)
        setCopied(false)
        setName('')
        setCanWrite(false)
      },
    })
  }

  const copyToken = async () => {
    if (!created) return
    try {
      await navigator.clipboard.writeText(created.token)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 2500)
    } catch {
      setCopied(false)
    }
  }

  const confirmRevoke = () => {
    if (!revoking) return
    revokeMutation.mutate(revoking.id, {
      onSuccess: () => setRevoking(null),
    })
  }

  if (isLoading && !keys) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    )
  }

  if (isError && !keys) {
    return (
      <div className="mx-auto max-w-md rounded-2xl border border-down/40 bg-down/10 p-8 text-center">
        <p className="text-sm text-text">Failed to load API keys.</p>
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

  const allKeys = keys ?? []
  const activeCount = allKeys.filter((key) => !key.revokedAtUtc).length

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold tracking-tight">API keys</h1>
        <p className="mt-1 max-w-2xl text-sm text-muted">
          Long-lived keys for integrations such as the AI assistant. Keys never expire; revoke
          them here when you no longer need them. Action-enabled keys can also redeploy apps and
          restart containers.
        </p>
      </div>

      {created && (
        <section className="rounded-2xl border border-warn/40 bg-warn/10 p-5">
          <div className="flex items-start gap-3">
            <Icon name="warning" className="mt-0.5 text-[22px] text-warn" />
            <div className="min-w-0 flex-1">
              <h2 className="text-sm font-semibold text-text">Copy this key now</h2>
              <p className="mt-1 text-xs leading-relaxed text-muted">
                MyHomeLab cannot show the full key again. Paste it into the integration that needs
                it, then keep it somewhere safe.
              </p>
              <div className="mt-3 flex flex-col gap-2 sm:flex-row">
                <code className="min-w-0 flex-1 overflow-x-auto whitespace-pre rounded-lg border border-border bg-background px-3 py-2 font-mono text-[12px] text-text">
                  {created.token}
                </code>
                <button
                  type="button"
                  onClick={copyToken}
                  className="inline-flex shrink-0 cursor-pointer items-center justify-center gap-2 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-hover"
                >
                  <Icon name={copied ? 'check' : 'content_copy'} className="text-[18px]" />
                  {copied ? 'Copied' : 'Copy key'}
                </button>
                <button
                  type="button"
                  onClick={() => setCreated(null)}
                  className="inline-flex shrink-0 cursor-pointer items-center justify-center rounded-lg border border-border px-4 py-2 text-sm text-muted transition hover:bg-surface-2 hover:text-text"
                >
                  Done
                </button>
              </div>
            </div>
          </div>
        </section>
      )}

      <section className="rounded-2xl border border-border bg-surface p-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h2 className="text-sm font-semibold text-text">Create a key</h2>
            <p className="mt-1 text-xs text-muted">
              Give the integration a name so you recognize it later.
            </p>
          </div>
          <span className="rounded-full bg-surface-2 px-2.5 py-1 text-xs text-muted">
            {activeCount} active
          </span>
        </div>
        <form onSubmit={submit} className="mt-4 flex flex-col gap-2 sm:flex-row">
          <input
            value={name}
            onChange={(event) => setName(event.target.value)}
            maxLength={80}
            placeholder="e.g. Vohithe AI assistant"
            aria-label="API key name"
            className={inputClass}
          />
          <button
            type="submit"
            disabled={createMutation.isPending || !name.trim()}
            className="inline-flex shrink-0 cursor-pointer items-center justify-center gap-2 rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-hover disabled:cursor-default disabled:opacity-50"
          >
            {createMutation.isPending ? (
              <span className="size-3.5 animate-spin rounded-full border-2 border-white/40 border-t-white" />
            ) : (
              <Icon name="add" className="text-[18px]" />
            )}
            Create key
          </button>
        </form>
        <label className="mt-3 flex items-start gap-2 text-xs leading-relaxed text-muted">
          <input
            type="checkbox"
            checked={canWrite}
            onChange={(event) => setCanWrite(event.target.checked)}
            className="mt-0.5 size-4 shrink-0 cursor-pointer accent-[#f97316]"
          />
          <span>
            Allow app actions — redeploy apps and restart their containers. Without this, the key
            is read-only.
          </span>
        </label>
        {createMutation.isError && (
          <p className="mt-2 text-xs text-down">{errorText(createMutation.error)}</p>
        )}
      </section>

      <section>
        <div className="mb-3 flex items-end justify-between gap-3">
          <div>
            <h2 className="text-sm font-semibold text-text">Existing keys</h2>
            <p className="mt-1 text-xs text-muted">
              Read access to apps, system metrics, and logs; action-enabled keys can also redeploy
              and restart containers.
            </p>
          </div>
        </div>
        {permissionMutation.isError && (
          <p className="mb-3 text-xs text-down">{errorText(permissionMutation.error)}</p>
        )}

        {allKeys.length === 0 ? (
          <div className="rounded-2xl border border-dashed border-border bg-surface/50 p-10 text-center">
            <Icon name="key" className="text-4xl text-muted/40" />
            <p className="mt-3 text-sm font-medium text-text">No API keys yet</p>
            <p className="mx-auto mt-1 max-w-sm text-xs leading-relaxed text-muted">
              Create one above when another app needs to read your MyHomeLab data.
            </p>
          </div>
        ) : (
          <div className="overflow-hidden rounded-2xl border border-border bg-surface">
            <ul className="divide-y divide-border/60">
              {allKeys.map((key) => (
                <li key={key.id} className="flex flex-wrap items-center gap-3 px-4 py-3">
                  <div className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-accent-soft text-accent">
                    <Icon name="key" className="text-[19px]" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="truncate text-sm font-semibold text-text">{key.name}</span>
                      <KeyStatus apiKey={key} />
                      <PermissionBadge apiKey={key} />
                    </div>
                    <p className="mt-0.5 truncate font-mono text-[11px] text-muted">
                      {key.prefix}... · created {timeAgo(key.createdAtUtc)} · last used{' '}
                      {key.lastUsedAtUtc ? timeAgo(key.lastUsedAtUtc) : 'never'}
                    </p>
                  </div>
                  {!key.revokedAtUtc && (
                    <button
                      type="button"
                      onClick={() =>
                        permissionMutation.mutate({ id: key.id, canWrite: !key.canWrite })
                      }
                      disabled={permissionMutation.isPending}
                      className="shrink-0 cursor-pointer rounded-lg border border-border px-3 py-1.5 text-xs font-semibold text-muted transition hover:bg-surface-2 hover:text-text disabled:cursor-default disabled:opacity-50"
                    >
                      {key.canWrite ? 'Make read-only' : 'Allow actions'}
                    </button>
                  )}
                  {!key.revokedAtUtc && (
                    <button
                      type="button"
                      onClick={() => setRevoking(key)}
                      className="shrink-0 cursor-pointer rounded-lg border border-down/30 px-3 py-1.5 text-xs font-semibold text-down transition hover:bg-down/10"
                    >
                      Revoke
                    </button>
                  )}
                </li>
              ))}
            </ul>
          </div>
        )}
      </section>

      <ConfirmDialog
        open={revoking !== null}
        title="Revoke this API key?"
        message={
          revoking
            ? `"${revoking.name}" will stop working immediately. This cannot be undone.`
            : ''
        }
        confirmLabel={revokeMutation.isPending ? 'Revoking...' : 'Revoke'}
        busy={revokeMutation.isPending}
        onConfirm={confirmRevoke}
        onCancel={() => {
          if (!revokeMutation.isPending) setRevoking(null)
        }}
      />
    </div>
  )
}
