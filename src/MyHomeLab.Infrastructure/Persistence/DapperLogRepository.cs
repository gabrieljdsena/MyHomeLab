using Dapper;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Domain.Entities;
using Npgsql;

namespace MyHomeLab.Infrastructure.Persistence;

internal sealed class DapperLogRepository(NpgsqlDataSource dataSource) : ILogRepository
{
    private const string Columns = """
        id          AS "Id",
        application AS "Application",
        log         AS "Log"
        """;

    public async Task<IReadOnlyList<LogEntry>> GetAllAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {Columns}
            FROM logs
            WHERE (@Application IS NULL OR @Application = '' OR application = @Application)
              AND (@Search IS NULL OR @Search = ''
                   OR application ILIKE '%' || @Search || '%'
                   OR log ILIKE '%' || @Search || '%')
            ORDER BY id DESC
            LIMIT @Limit;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<LogRow>(
            sql,
            new { query.Application, query.Search, query.Limit });

        return rows.Select(ToEntity).ToArray();
    }

    public async Task<LogEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {Columns}
            FROM logs
            WHERE id = @Id;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<LogRow>(sql, new { Id = id });
        return row is null ? null : ToEntity(row);
    }

    private static LogEntry ToEntity(LogRow row) =>
        LogEntry.Restore(row.Id, row.Application, row.Log);

    private sealed class LogRow
    {
        public int Id { get; set; }
        public string Application { get; set; } = string.Empty;
        public string Log { get; set; } = string.Empty;
    }
}
