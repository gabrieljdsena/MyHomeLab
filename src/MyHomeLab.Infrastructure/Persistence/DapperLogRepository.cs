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

    public async Task<(IReadOnlyList<LogEntry> Items, int TotalCount)> GetPagedAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        const string where = """
            WHERE (@Application IS NULL OR @Application = '' OR application = @Application)
              AND (@Search IS NULL OR @Search = ''
                   OR application ILIKE '%' || @Search || '%'
                   OR log ILIKE '%' || @Search || '%')
            """;
        var sql = $"""
            SELECT COUNT(*)
            FROM logs
            {where};
            SELECT {Columns}
            FROM logs
            {where}
            ORDER BY id DESC
            LIMIT @PageSize OFFSET @Offset;
            """;

        var offset = (query.Page - 1) * query.PageSize;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var grid = await connection.QueryMultipleAsync(
            sql,
            new
            {
                query.Application,
                query.Search,
                query.PageSize,
                Offset = offset,
            });

        var totalCount = await grid.ReadSingleAsync<int>();
        var rows = await grid.ReadAsync<LogRow>();
        return (rows.Select(ToEntity).ToArray(), totalCount);
    }

    public async Task<IReadOnlyList<string>> GetApplicationsAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT application
            FROM logs
            WHERE application IS NOT NULL AND application <> ''
            ORDER BY application;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var applications = await connection.QueryAsync<string>(sql);
        return applications.ToArray();
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
