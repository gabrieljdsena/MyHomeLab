using Dapper;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;
using Npgsql;

namespace MyHomeLab.Infrastructure.Persistence;

internal sealed class DapperHealthHistoryRepository(NpgsqlDataSource dataSource) : IHealthHistoryRepository
{
    public async Task AddAsync(AppHealthSample sample, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO app_health_checks (app_id, status, latency_ms, checked_at)
            VALUES (@AppId, @Status, @LatencyMs, @CheckedAt);
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            sql,
            new
            {
                AppId = sample.AppId.Value,
                Status = sample.Status.ToString().ToLowerInvariant(),
                sample.LatencyMs,
                CheckedAt = sample.CheckedAtUtc,
            });
    }

    public async Task<IReadOnlyList<AppHealthSample>> GetRecentAsync(HealthHistoryQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id         AS "Id",
                app_id     AS "AppId",
                status     AS "Status",
                latency_ms AS "LatencyMs",
                checked_at AS "CheckedAtUtc"
            FROM app_health_checks
            WHERE app_id = @AppId
              AND (@SinceUtc IS NULL OR checked_at >= @SinceUtc)
            ORDER BY checked_at DESC
            LIMIT @Limit;
            """;

        var limit = query.Limit < 1 ? 1 : query.Limit > 1000 ? 1000 : query.Limit;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<HealthRow>(
            sql,
            new
            {
                AppId = query.AppId.Value,
                SinceUtc = query.SinceUtc,
                Limit = limit,
            });

        return rows.Select(ToEntity).OrderBy(s => s.CheckedAtUtc).ToArray();
    }

    private static AppHealthSample ToEntity(HealthRow row) =>
        new(
            row.Id,
            AppId.From(row.AppId),
            Enum.Parse<AppHealthStatus>(row.Status, ignoreCase: true),
            row.LatencyMs,
            row.CheckedAtUtc);

    private sealed class HealthRow
    {
        public long Id { get; set; }
        public Guid AppId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? LatencyMs { get; set; }
        public DateTime CheckedAtUtc { get; set; }
    }
}
