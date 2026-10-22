import { useState, type MouseEvent as ReactMouseEvent } from 'react'
import type { PostgresCatalog, PostgresRelation } from '../../api/system'
import { ContextMenu } from '../../components/ContextMenu'
import { Icon, Spinner } from '../../components/Icon'
import { filterRelations, relationIcon, relationLabel } from './schemaHelpers'
import { databaseMenuItems, relationMenuItems, type TreeMenuAction } from './treeMenu'

export function DatabaseTree({
  catalog,
  isLoading,
  isError,
  target,
  onSelect,
  onInsertSql,
  onSetTarget,
  onRefresh,
}: {
  catalog?: PostgresCatalog
  isLoading: boolean
  isError: boolean
  target: string | null
  onSelect: (database: string, relation: PostgresRelation) => void
  onInsertSql: (database: string, sql: string) => void
  onSetTarget: (database: string) => void
  onRefresh: () => void
}) {
  const [filter, setFilter] = useState('')
  const [expanded, setExpanded] = useState<Record<string, boolean>>({})
  const [menu, setMenu] = useState<{
    x: number
    y: number
    database: string
    items: TreeMenuAction[]
  } | null>(null)
  const filtering = filter.trim().length > 0

  const openMenu = (event: ReactMouseEvent, database: string, items: TreeMenuAction[]) => {
    event.preventDefault()
    setMenu({ x: event.clientX, y: event.clientY, database, items })
  }

  const pickMenu = (id: string) => {
    if (!menu) return
    const action = menu.items.find((item) => item.id === id)
    setMenu(null)
    if (!action) return

    if (action.copy) {
      void navigator.clipboard.writeText(action.copy).catch(() => {})
    } else if (action.target) {
      onSetTarget(action.target)
    } else if (action.sql) {
      onInsertSql(menu.database, action.sql)
    }
  }

  return (
    <div className="flex min-h-0 flex-col overflow-hidden rounded-xl border border-border bg-surface-2/40">
      <div className="flex items-center gap-2 border-b border-border/60 px-3 py-2">
        <p className="flex-1 text-[10.5px] font-semibold uppercase tracking-wider text-muted">Databases</p>
        <button
          type="button"
          onClick={onRefresh}
          title="Refresh schema"
          className="cursor-pointer text-muted/60 transition hover:text-text"
        >
          <Icon name="refresh" className="text-[15px]" />
        </button>
      </div>

      <div className="px-2.5 pt-2">
        <input
          value={filter}
          onChange={(event) => setFilter(event.target.value)}
          placeholder="Filter tables…"
          spellCheck={false}
          className="w-full rounded-lg border border-border bg-surface px-2 py-1 text-[11.5px] text-text outline-none transition placeholder:text-muted/40 focus:border-accent/60"
        />
      </div>

      <div className="max-h-72 overflow-auto p-1.5 lg:max-h-[30rem]">
        {isLoading && !catalog && (
          <div className="flex justify-center py-4">
            <Spinner />
          </div>
        )}

        {isError && !catalog && <p className="px-2 py-3 text-[11.5px] text-muted/70">Schema unavailable.</p>}

        {catalog?.databases.length === 0 && (
          <p className="px-2 py-3 text-[11.5px] text-muted/70">No databases.</p>
        )}

        {catalog?.databases.map((database) => {
          const relations = filterRelations(database.relations, filter)
          const isOpen = filtering ? relations.length > 0 : (expanded[database.name] ?? database.name === target)
          if (filtering && relations.length === 0) return null

          return (
            <div key={database.name}>
              <button
                type="button"
                onClick={() => setExpanded((previous) => ({ ...previous, [database.name]: !isOpen }))}
                onContextMenu={(event) => openMenu(event, database.name, databaseMenuItems(database))}
                className="flex w-full cursor-pointer items-center gap-1.5 rounded-lg px-1.5 py-1.5 text-left transition hover:bg-surface-2"
              >
                <Icon
                  name="chevron_right"
                  className={`text-[15px] text-muted/60 transition-transform ${isOpen ? 'rotate-90' : ''}`}
                />
                <Icon name="database" className="text-[15px] text-muted" />
                <span
                  className={`min-w-0 flex-1 truncate font-mono text-[11.5px] ${
                    database.name === target ? 'text-accent' : 'text-text'
                  }`}
                >
                  {database.name}
                </span>
                <span className="shrink-0 text-[10px] tabular-nums text-muted/50">{database.relations.length}</span>
              </button>

              {isOpen && (
                <ul>
                  {relations.map((relation) => (
                    <li key={`${relation.schema}.${relation.name}.${relation.kind}`}>
                      <button
                        type="button"
                        onClick={() => onSelect(database.name, relation)}
                        onContextMenu={(event) => openMenu(event, database.name, relationMenuItems(relation))}
                        title={`${relationLabel(relation)} (${relation.kind.replaceAll('_', ' ')})`}
                        className="flex w-full cursor-pointer items-center gap-1.5 rounded-lg py-1 pl-6 pr-1.5 text-left transition hover:bg-surface-2 hover:text-accent"
                      >
                        <Icon name={relationIcon(relation.kind)} className="text-[14px] text-muted/60" />
                        <span className="min-w-0 flex-1 truncate font-mono text-[11px] text-muted">
                          {relationLabel(relation)}
                        </span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )
        })}
      </div>

      {menu && (
        <ContextMenu
          x={menu.x}
          y={menu.y}
          items={menu.items}
          onPick={pickMenu}
          onClose={() => setMenu(null)}
        />
      )}
    </div>
  )
}
