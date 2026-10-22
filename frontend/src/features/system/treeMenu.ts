import type { PostgresDatabase, PostgresRelation } from '../../api/system'
import { countQuery, listTablesQuery, qualifiedName, tableQuery, tableSizesQuery } from './schemaHelpers'

export interface TreeMenuAction {
  id: string
  label: string
  icon: string
  sql?: string
  target?: string
  copy?: string
}

export function databaseMenuItems(database: PostgresDatabase): TreeMenuAction[] {
  return [
    { id: 'set-target', label: 'Set as run target', icon: 'my_location', target: database.name },
    { id: 'list-tables', label: 'List tables', icon: 'format_list_bulleted', sql: listTablesQuery() },
    { id: 'table-sizes', label: 'Table sizes', icon: 'straighten', sql: tableSizesQuery() },
  ]
}

export function relationMenuItems(relation: PostgresRelation): TreeMenuAction[] {
  return [
    { id: 'select-all', label: 'SELECT * (LIMIT 50)', icon: 'table', sql: tableQuery(relation.schema, relation.name) },
    { id: 'select-count', label: 'SELECT COUNT(*)', icon: 'functions', sql: countQuery(relation.schema, relation.name) },
    { id: 'copy-name', label: 'Copy qualified name', icon: 'content_copy', copy: qualifiedName(relation.schema, relation.name) },
  ]
}
