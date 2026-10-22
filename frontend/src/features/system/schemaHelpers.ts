import type { PostgresRelation } from '../../api/system'

const SIMPLE_IDENTIFIER = /^[a-z_][a-z0-9_]*$/

export function quoteIdent(name: string): string {
  if (SIMPLE_IDENTIFIER.test(name)) return name
  return `"${name.replaceAll('"', '""')}"`
}

export function tableQuery(schema: string, name: string, limit = 50): string {
  return `SELECT * FROM ${quoteIdent(schema)}.${quoteIdent(name)} LIMIT ${limit};`
}

export function countQuery(schema: string, name: string): string {
  return `SELECT COUNT(*) FROM ${quoteIdent(schema)}.${quoteIdent(name)};`
}

export function qualifiedName(schema: string, name: string): string {
  return `${quoteIdent(schema)}.${quoteIdent(name)}`
}

export function listTablesQuery(): string {
  return "SELECT schemaname, tablename FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog', 'information_schema') ORDER BY schemaname, tablename;"
}

export function tableSizesQuery(): string {
  return 'SELECT schemaname, relname, pg_size_pretty(pg_total_relation_size(relid)) AS size FROM pg_catalog.pg_statio_user_tables ORDER BY pg_total_relation_size(relid) DESC;'
}

export function relationLabel(relation: Pick<PostgresRelation, 'schema' | 'name'>): string {
  return relation.schema === 'public' ? relation.name : `${relation.schema}.${relation.name}`
}

export function filterRelations(relations: PostgresRelation[], filter: string): PostgresRelation[] {
  const needle = filter.trim().toLowerCase()
  if (needle.length === 0) return relations
  return relations.filter((relation) =>
    `${relation.schema}.${relation.name}`.toLowerCase().includes(needle),
  )
}

export function relationIcon(kind: string): string {
  switch (kind) {
    case 'view':
      return 'visibility'
    case 'materialized_view':
      return 'table_view'
    default:
      return 'table'
  }
}
