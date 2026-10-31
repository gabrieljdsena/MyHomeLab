import { describe, expect, it } from 'vitest'
import type { PostgresRelation } from '../../api/system'
import {
  countQuery,
  filterRelations,
  listTablesQuery,
  qualifiedName,
  quoteIdent,
  relationIcon,
  relationLabel,
  tableQuery,
  tableSizesQuery,
} from './schemaHelpers'

const relation = (schema: string, name: string, kind: PostgresRelation['kind'] = 'table'): PostgresRelation => ({
  schema,
  name,
  kind,
})

describe('quoteIdent', () => {
  it('leaves simple identifiers unquoted', () => {
    expect(quoteIdent('apps')).toBe('apps')
    expect(quoteIdent('_x1')).toBe('_x1')
  })

  it('quotes identifiers that need it', () => {
    expect(quoteIdent('Apps')).toBe('"Apps"')
    expect(quoteIdent('1abc')).toBe('"1abc"')
    expect(quoteIdent('with space')).toBe('"with space"')
  })

  it('escapes embedded quotes', () => {
    expect(quoteIdent('we"ird')).toBe('"we""ird"')
  })
})

describe('tableQuery', () => {
  it('builds a SELECT template', () => {
    expect(tableQuery('public', 'apps')).toBe('SELECT * FROM public.apps LIMIT 50;')
  })

  it('honors a custom limit and quotes when needed', () => {
    expect(tableQuery('audit', 'Log Entries', 10)).toBe('SELECT * FROM audit."Log Entries" LIMIT 10;')
  })
})

describe('countQuery', () => {
  it('builds a COUNT template', () => {
    expect(countQuery('public', 'apps')).toBe('SELECT COUNT(*) FROM public.apps;')
  })

  it('quotes when needed', () => {
    expect(countQuery('audit', 'Log Entries')).toBe('SELECT COUNT(*) FROM audit."Log Entries";')
  })
})

describe('qualifiedName', () => {
  it('qualifies and quotes identifiers', () => {
    expect(qualifiedName('public', 'apps')).toBe('public.apps')
    expect(qualifiedName('audit', 'Log Entries')).toBe('audit."Log Entries"')
  })
})

describe('listTablesQuery', () => {
  it('reads only user tables from the catalog', () => {
    const sql = listTablesQuery()
    expect(sql).toContain('pg_catalog.pg_tables')
    expect(sql).toContain("NOT IN ('pg_catalog', 'information_schema')")
  })
})

describe('tableSizesQuery', () => {
  it('lists user tables ordered by size', () => {
    const sql = tableSizesQuery()
    expect(sql).toContain('pg_catalog.pg_statio_user_tables')
    expect(sql).toContain('ORDER BY pg_total_relation_size(relid) DESC')
  })
})

describe('relationLabel', () => {
  it('hides the public schema', () => {
    expect(relationLabel({ schema: 'public', name: 'apps' })).toBe('apps')
  })

  it('shows other schemas', () => {
    expect(relationLabel({ schema: 'audit', name: 'logs' })).toBe('audit.logs')
  })
})

describe('filterRelations', () => {
  const relations = [relation('public', 'apps'), relation('audit', 'event_log'), relation('public', 'logs')]

  it('returns the same list for an empty filter', () => {
    expect(filterRelations(relations, '  ')).toBe(relations)
  })

  it('matches names case-insensitively', () => {
    expect(filterRelations(relations, 'LOG').map((item) => item.name)).toEqual(['event_log', 'logs'])
  })

  it('matches schema-qualified needles', () => {
    expect(filterRelations(relations, 'audit.').map((item) => item.name)).toEqual(['event_log'])
  })

  it('returns an empty list when nothing matches', () => {
    expect(filterRelations(relations, 'zzz')).toEqual([])
  })
})

describe('relationIcon', () => {
  it('maps kinds to icons', () => {
    expect(relationIcon('table')).toBe('table')
    expect(relationIcon('view')).toBe('visibility')
    expect(relationIcon('materialized_view')).toBe('table_view')
    expect(relationIcon('other')).toBe('table')
  })
})
