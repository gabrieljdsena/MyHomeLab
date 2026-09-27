using Dapper;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using Npgsql;

namespace MyHomeLab.Infrastructure.Persistence;

/// <summary>
/// Reads the hub's own database through the shared <see cref="NpgsqlDataSource"/>. The hub
/// connects as the bootstrap superuser, so every view here is readable without extra grants —
/// if that ever stops being true the query still succeeds and the unreadable fields come back
/// null, which <see cref="Map"/> turns into zero rather than failing the whole snapshot.
/// </summary>
public sealed class DapperPostgresMetricsReader(NpgsqlDataSource dataSource) : IPostgresMetricsReader
{
    private const string Sql = """
        SELECT
            current_setting('server_version')                                   AS "Version",
            current_database()                                                  AS "DatabaseName",
            extract(epoch FROM now() - pg_postmaster_start_time())::bigint      AS "UptimeSeconds",
            pg_database_size(current_database())                                AS "SizeBytes",
            a.ConnUsed,
            a.ConnMax,
            a.ConnActive,
            a.ConnIdle,
            a.LockWaiters,
            a.LongestQuerySeconds,
            d.xact_commit                                                       AS "TransactionsCommitted",
            d.xact_rollback                                                     AS "TransactionsRolledBack",
            d.deadlocks                                                          AS "Deadlocks",
            d.blks_read                                                         AS "BlocksRead",
            d.blks_hit                                                          AS "BlocksHit",
            d.temp_bytes                                                        AS "TempBytes",
            (SELECT coalesce(sum(wal_bytes), 0) FROM pg_stat_wal)               AS "WalBytes",
            (SELECT num_done FROM pg_stat_checkpointer)                        AS "CheckpointsDone"
        FROM pg_stat_database d
        CROSS JOIN LATERAL (
            SELECT
                count(*)::int                                                   AS ConnUsed,
                coalesce(
                    (SELECT setting::int FROM pg_settings WHERE name = 'max_connections'),
                    0)                                                          AS ConnMax,
                count(*) FILTER (WHERE state = 'active')::int                   AS ConnActive,
                count(*) FILTER (WHERE state = 'idle')::int                     AS ConnIdle,
                count(*) FILTER (WHERE wait_event_type = 'Lock')::int          AS LockWaiters,
                -- Our own sampling query is itself an active query on this database, so it
                -- would otherwise always be reported as the longest running one.
                coalesce(
                    max(extract(epoch FROM (now() - query_start)))
                        FILTER (WHERE state = 'active' AND pid <> pg_backend_pid()),
                    0)::float8                                                   AS LongestQuerySeconds
            FROM pg_stat_activity
            WHERE datname = current_database()
        ) a
        WHERE d.datname = current_database()
        """;

    public async Task<PostgresCounters> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            Sql, cancellationToken: cancellationToken));

        return Map(row);
    }

    private static PostgresCounters Map(Row? row) => new(
        row?.Version ?? "unknown",
        row?.DatabaseName ?? "unknown",
        row?.UptimeSeconds ?? 0,
        row?.SizeBytes ?? 0,
        row?.ConnUsed ?? 0,
        row?.ConnMax ?? 0,
        row?.ConnActive ?? 0,
        row?.ConnIdle ?? 0,
        row?.LockWaiters ?? 0,
        row?.LongestQuerySeconds ?? 0,
        row?.TransactionsCommitted ?? 0,
        row?.TransactionsRolledBack ?? 0,
        row?.Deadlocks ?? 0,
        row?.BlocksRead ?? 0,
        row?.BlocksHit ?? 0,
        row?.TempBytes ?? 0,
        row?.WalBytes ?? 0,
        row?.CheckpointsDone ?? 0,
        DateTime.UtcNow);

    // Npgsql maps PG bigint to long, but the int4 activity counters come back as int and the
    // sum() over pg_stat_wal is numeric; normalise everything nullable so Map stays total.
    private sealed class Row
    {
        public string? Version { get; set; }
        public string? DatabaseName { get; set; }
        public long? UptimeSeconds { get; set; }
        public long? SizeBytes { get; set; }
        public int? ConnUsed { get; set; }
        public int? ConnMax { get; set; }
        public int? ConnActive { get; set; }
        public int? ConnIdle { get; set; }
        public int? LockWaiters { get; set; }
        public double? LongestQuerySeconds { get; set; }
        public long? TransactionsCommitted { get; set; }
        public long? TransactionsRolledBack { get; set; }
        public long? Deadlocks { get; set; }
        public long? BlocksRead { get; set; }
        public long? BlocksHit { get; set; }
        public long? TempBytes { get; set; }
        public long? WalBytes { get; set; }
        public long? CheckpointsDone { get; set; }
    }
}
