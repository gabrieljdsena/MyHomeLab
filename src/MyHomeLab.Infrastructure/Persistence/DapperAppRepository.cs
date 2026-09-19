using Dapper;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Errors;
using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;
using Npgsql;

namespace MyHomeLab.Infrastructure.Persistence;

internal sealed class DapperAppRepository(NpgsqlDataSource dataSource) : IAppRepository
{
    private const string Columns = """
        id                       AS "Id",
        name                     AS "Name",
        description              AS "Description",
        url                      AS "Url",
        icon                     AS "Icon",
        category                 AS "Category",
        port                     AS "Port",
        tags                     AS "Tags",
        health_check_enabled     AS "HealthCheckEnabled",
        health_check_interval_ms AS "HealthCheckIntervalMs",
        health_status            AS "HealthStatus",
        last_health_check        AS "LastHealthCheckUtc",
        last_latency_ms          AS "LastLatencyMs",
        is_enabled               AS "IsEnabled",
        sort_order               AS "SortOrder",
        created_at               AS "CreatedAtUtc",
        updated_at               AS "UpdatedAtUtc"
        """;

    public async Task<IReadOnlyList<App>> GetAllAsync(AppQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {Columns}
            FROM apps
            WHERE (@Search IS NULL OR @Search = '' OR name ILIKE '%' || @Search || '%'
                   OR EXISTS (SELECT 1 FROM unnest(tags) AS tag WHERE tag ILIKE '%' || @Search || '%'))
              AND (@Category IS NULL OR @Category = '' OR category = @Category)
              AND (@EnabledOnly IS NULL OR is_enabled = @EnabledOnly)
            ORDER BY category, sort_order, name;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AppRow>(
            sql,
            new
            {
                query.Search,
                query.Category,
                query.EnabledOnly,
            });

        return rows.Select(ToEntity).ToArray();
    }

    public async Task<App?> GetByIdAsync(AppId id, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {Columns}
            FROM apps
            WHERE id = @Id;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AppRow>(sql, new { Id = id.Value });
        return row is null ? null : ToEntity(row);
    }

    public async Task<App?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {Columns}
            FROM apps
            WHERE lower(name) = lower(@Name)
            LIMIT 1;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<AppRow>(sql, new { Name = name });
        return row is null ? null : ToEntity(row);
    }

    public async Task AddAsync(App app, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO apps (
                id, name, description, url, icon, category, port, tags,
                health_check_enabled, health_check_interval_ms, health_status,
                last_health_check, last_latency_ms, is_enabled, sort_order,
                created_at, updated_at)
            VALUES (
                @Id, @Name, @Description, @Url, @Icon, @Category, @Port, @Tags,
                @HealthCheckEnabled, @HealthCheckIntervalMs, @HealthStatus,
                @LastHealthCheckUtc, @LastLatencyMs, @IsEnabled, @SortOrder,
                @CreatedAtUtc, @UpdatedAtUtc);
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(sql, ToParameters(app));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new AppConflictException($"An app named '{app.Name}' already exists.");
        }
    }

    public async Task UpdateAsync(App app, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE apps
            SET name                     = @Name,
                description              = @Description,
                url                      = @Url,
                icon                     = @Icon,
                category                 = @Category,
                port                     = @Port,
                tags                     = @Tags,
                health_check_enabled     = @HealthCheckEnabled,
                health_check_interval_ms = @HealthCheckIntervalMs,
                is_enabled               = @IsEnabled,
                sort_order               = @SortOrder,
                updated_at               = @UpdatedAtUtc
            WHERE id = @Id;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(sql, ToParameters(app));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new AppConflictException($"An app named '{app.Name}' already exists.");
        }
    }

    public async Task DeleteAsync(AppId id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM apps WHERE id = @Id;";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { Id = id.Value });
    }

    public async Task UpdateHealthAsync(App app, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE apps
            SET health_status     = @HealthStatus,
                last_latency_ms   = @LastLatencyMs,
                last_health_check = @LastHealthCheckUtc
            WHERE id = @Id;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            sql,
            new
            {
                Id = app.Id.Value,
                HealthStatus = app.HealthStatus.ToString().ToLowerInvariant(),
                LastLatencyMs = app.LastLatencyMs,
                LastHealthCheckUtc = app.LastHealthCheckUtc,
            });
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT category
            FROM apps
            WHERE category IS NOT NULL AND category <> ''
            ORDER BY category;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var categories = await connection.QueryAsync<string>(sql);
        return categories.ToArray();
    }

    private static object ToParameters(App app) =>
        new
        {
            Id = app.Id.Value,
            Name = app.Name,
            Description = app.Description,
            Url = app.Url,
            Icon = app.Icon,
            Category = app.Category,
            Port = app.Port,
            Tags = app.Tags,
            HealthCheckEnabled = app.HealthCheckEnabled,
            HealthCheckIntervalMs = app.HealthCheckIntervalMs,
            HealthStatus = app.HealthStatus.ToString().ToLowerInvariant(),
            LastHealthCheckUtc = app.LastHealthCheckUtc,
            LastLatencyMs = app.LastLatencyMs,
            IsEnabled = app.IsEnabled,
            SortOrder = app.SortOrder,
            CreatedAtUtc = app.CreatedAtUtc,
            UpdatedAtUtc = app.UpdatedAtUtc,
        };

    private static App ToEntity(AppRow row) =>
        App.Restore(
            AppId.From(row.Id),
            row.Name,
            row.Description,
            row.Url,
            row.Icon,
            row.Category,
            row.Port,
            row.Tags,
            row.HealthCheckEnabled,
            row.HealthCheckIntervalMs,
            Enum.Parse<AppHealthStatus>(row.HealthStatus, ignoreCase: true),
            row.LastHealthCheckUtc,
            row.LastLatencyMs,
            row.IsEnabled,
            row.SortOrder,
            row.CreatedAtUtc,
            row.UpdatedAtUtc);

    private sealed class AppRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int? Port { get; set; }
        public string[] Tags { get; set; } = [];
        public bool HealthCheckEnabled { get; set; }
        public int HealthCheckIntervalMs { get; set; }
        public string HealthStatus { get; set; } = string.Empty;
        public DateTime? LastHealthCheckUtc { get; set; }
        public int? LastLatencyMs { get; set; }
        public bool IsEnabled { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
