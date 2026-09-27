using Dapper;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Errors;
using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;
using Npgsql;

namespace MyHomeLab.Infrastructure.Persistence;

internal sealed class DapperMachineRepository(NpgsqlDataSource dataSource) : IMachineRepository
{
    private const string Columns = """
        id                 AS "Id",
        name               AS "Name",
        description        AS "Description",
        hostname           AS "Hostname",
        icon               AS "Icon",
        is_enabled         AS "IsEnabled",
        sort_order         AS "SortOrder",
        reachability       AS "Reachability",
        last_seen          AS "LastSeenUtc",
        last_latency_ms    AS "LastLatencyMs",
        last_ip            AS "LastIpAddress",
        last_mac           AS "LastMacAddress",
        created_at         AS "CreatedAtUtc",
        updated_at         AS "UpdatedAtUtc"
        """;

    public async Task<IReadOnlyList<Machine>> GetAllAsync(MachineQuery query, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {Columns}
            FROM lan_machines
            WHERE (@Search IS NULL OR @Search = '' OR name ILIKE '%' || @Search || '%'
                   OR hostname ILIKE '%' || @Search || '%')
              AND (@EnabledOnly IS NULL OR is_enabled = @EnabledOnly)
            ORDER BY sort_order, name;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MachineRow>(
            sql,
            new { query.Search, query.EnabledOnly });

        return rows.Select(ToEntity).ToArray();
    }

    public async Task<Machine?> GetByIdAsync(MachineId id, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {Columns}
            FROM lan_machines
            WHERE id = @Id;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<MachineRow>(sql, new { Id = id.Value });
        return row is null ? null : ToEntity(row);
    }

    public async Task<Machine?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        const string sql = $"""
            SELECT {Columns}
            FROM lan_machines
            WHERE lower(name) = lower(@Name)
            LIMIT 1;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<MachineRow>(sql, new { Name = name });
        return row is null ? null : ToEntity(row);
    }

    public async Task AddAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO lan_machines (
                id, name, description, hostname, icon,
                is_enabled, sort_order, reachability, last_seen, last_latency_ms,
                created_at, updated_at)
            VALUES (
                @Id, @Name, @Description, @Hostname, @Icon,
                @IsEnabled, @SortOrder, @Reachability, @LastSeenUtc, @LastLatencyMs,
                @CreatedAtUtc, @UpdatedAtUtc);
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(sql, ToParameters(machine));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new MachineConflictException($"A machine named '{machine.Name}' already exists.");
        }
    }

    public async Task UpdateAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE lan_machines
            SET name             = @Name,
                description      = @Description,
                hostname         = @Hostname,
                icon             = @Icon,
                is_enabled       = @IsEnabled,
                sort_order       = @SortOrder,
                updated_at       = @UpdatedAtUtc
            WHERE id = @Id;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(sql, ToParameters(machine));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new MachineConflictException($"A machine named '{machine.Name}' already exists.");
        }
    }

    public async Task DeleteAsync(MachineId id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM lan_machines WHERE id = @Id;";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { Id = id.Value });
    }

    public async Task UpdateReachabilityAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE lan_machines
            SET reachability    = @Reachability,
                last_seen       = @LastSeenUtc,
                last_latency_ms = @LastLatencyMs,
                last_ip         = @LastIpAddress,
                last_mac        = @LastMacAddress
            WHERE id = @Id;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            sql,
            new
            {
                Id = machine.Id.Value,
                Reachability = machine.Reachability.ToString().ToLowerInvariant(),
                LastSeenUtc = machine.LastSeenUtc,
                LastLatencyMs = machine.LastLatencyMs,
                LastIpAddress = machine.LastIpAddress,
                LastMacAddress = machine.LastMacAddress,
            });
    }

    private static object ToParameters(Machine machine) =>
        new
        {
            Id = machine.Id.Value, Name = machine.Name, Description = machine.Description,
            Hostname = machine.Hostname, Icon = machine.Icon,
            IsEnabled = machine.IsEnabled, SortOrder = machine.SortOrder,
            Reachability = machine.Reachability.ToString().ToLowerInvariant(),
            LastSeenUtc = machine.LastSeenUtc, LastLatencyMs = machine.LastLatencyMs,
            CreatedAtUtc = machine.CreatedAtUtc, UpdatedAtUtc = machine.UpdatedAtUtc,
        };

    private static Machine ToEntity(MachineRow row) =>
        Machine.Restore(
            MachineId.From(row.Id), row.Name, row.Description, row.Hostname, row.Icon,
            row.IsEnabled, row.SortOrder,
            Enum.Parse<MachineReachability>(row.Reachability, ignoreCase: true),
            row.LastSeenUtc, row.LastLatencyMs, row.LastIpAddress, row.LastMacAddress,
            row.CreatedAtUtc, row.UpdatedAtUtc);

    private sealed class MachineRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public int SortOrder { get; set; }
        public string Reachability { get; set; } = string.Empty;
        public DateTime? LastSeenUtc { get; set; }
        public int? LastLatencyMs { get; set; }
        public string? LastIpAddress { get; set; }
        public string? LastMacAddress { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
