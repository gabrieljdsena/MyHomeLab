using Dapper;
using Microsoft.Extensions.Logging;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using Npgsql;

namespace MyHomeLab.Infrastructure.Persistence;

public sealed class NpgsqlPostgresCatalogReader(
    NpgsqlDataSource dataSource,
    NpgsqlConnectionSource connectionSource,
    ILogger<NpgsqlPostgresCatalogReader> logger) : IPostgresCatalogReader
{
    private const int ConnectionTimeoutSeconds = 5;

    private const string CurrentDatabaseSql = "SELECT current_database()";

    private const string DatabasesSql = """
        SELECT datname
        FROM pg_database
        WHERE NOT datistemplate AND datallowconn
        ORDER BY datname
        """;

    private const string RelationsSql = """
        SELECT schemaname, relname, kind
        FROM (
            SELECT schemaname, tablename AS relname, 'table' AS kind
            FROM pg_tables
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
            UNION ALL
            SELECT schemaname, viewname, 'view'
            FROM pg_views
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
            UNION ALL
            SELECT schemaname, matviewname, 'materialized_view'
            FROM pg_matviews
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
        ) r
        ORDER BY schemaname, relname
        """;

    public async Task<PostgresCatalogDto> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var current = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            CurrentDatabaseSql, cancellationToken: cancellationToken));
        var databaseNames = await connection.QueryAsync<string>(new CommandDefinition(
            DatabasesSql, cancellationToken: cancellationToken));

        var databases = new List<PostgresDatabaseDto>();
        foreach (var name in databaseNames)
        {
            databases.Add(new PostgresDatabaseDto(name, await ReadRelationsAsync(name, cancellationToken)));
        }

        return new PostgresCatalogDto(current ?? string.Empty, databases);
    }

    private async Task<IReadOnlyList<PostgresRelationDto>> ReadRelationsAsync(string database, CancellationToken cancellationToken)
    {
        try
        {
            // The catalog needs one connection per database: pg_tables/pg_views only
            // describe the database they're queried from.
            await using var connection = new NpgsqlConnection(
                connectionSource.ForDatabase(database, ConnectionTimeoutSeconds));
            await connection.OpenAsync(cancellationToken);
            var rows = await connection.QueryAsync<RelationRow>(new CommandDefinition(
                RelationsSql, cancellationToken: cancellationToken));

            return rows.Select(row => new PostgresRelationDto(row.Schemaname, row.Relname, row.Kind)).ToList();
        }
        catch (NpgsqlException ex)
        {
            logger.LogWarning(ex, "Could not list relations of database {Database}; listing it as empty.", database);
            return [];
        }
    }

    private sealed class RelationRow
    {
        public string Schemaname { get; set; } = string.Empty;
        public string Relname { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
    }
}
