import { describe, expect, it } from 'vitest'
import { databaseMenuItems, relationMenuItems } from './treeMenu'

describe('databaseMenuItems', () => {
  it('offers target switch and catalog queries', () => {
    const items = databaseMenuItems({ name: 'hub', relations: [] })

    expect(items.map((item) => item.id)).toEqual(['set-target', 'list-tables', 'table-sizes'])
    expect(items[0].target).toBe('hub')
    expect(items[0].sql).toBeUndefined()
    expect(items[1].sql).toContain('pg_catalog.pg_tables')
    expect(items[2].sql).toContain('pg_catalog.pg_statio_user_tables')
  })
})

describe('relationMenuItems', () => {
  it('builds select templates and copy value', () => {
    const items = relationMenuItems({ schema: 'public', name: 'apps', kind: 'table' })

    expect(items.map((item) => item.id)).toEqual(['select-all', 'select-count', 'copy-name'])
    expect(items[0].sql).toBe('SELECT * FROM public.apps LIMIT 50;')
    expect(items[1].sql).toBe('SELECT COUNT(*) FROM public.apps;')
    expect(items[2].copy).toBe('public.apps')
  })

  it('quotes identifiers that need it', () => {
    const items = relationMenuItems({ schema: 'audit', name: 'Log Entries', kind: 'view' })

    expect(items[0].sql).toBe('SELECT * FROM audit."Log Entries" LIMIT 50;')
    expect(items[2].copy).toBe('audit."Log Entries"')
  })
})
