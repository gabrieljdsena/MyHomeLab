using Dapper;
using MyHomeLab.Application.Abstractions;
using Npgsql;

namespace MyHomeLab.Infrastructure.Persistence;

internal sealed class DapperApiKeyRepository(NpgsqlDataSource dataSource) : IApiKeyRepository
{
    private const string SelectColumns = """
        id           AS "Id",
        name         AS "Name",
        prefix       AS "Prefix",
        token_hash   AS "TokenHash",
        created_at   AS "CreatedAtUtc",
        last_used_at AS "LastUsedAtUtc",
        revoked_at   AS "RevokedAtUtc",
        can_write    AS "CanWrite"
        """;

    public async Task<IReadOnlyList<ApiKeyRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM api_keys ORDER BY created_at DESC;";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ApiKeyRecord>(sql);
        return rows.ToArray();
    }

    public async Task<ApiKeyRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM api_keys WHERE id = @Id;";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ApiKeyRecord>(sql, new { Id = id });
    }

    public async Task<ApiKeyRecord?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM api_keys WHERE token_hash = @TokenHash;";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ApiKeyRecord>(sql, new { TokenHash = tokenHash });
    }

    public async Task AddAsync(ApiKeyRecord key, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO api_keys (id, name, prefix, token_hash, created_at, can_write)
            VALUES (@Id, @Name, @Prefix, @TokenHash, @CreatedAtUtc, @CanWrite);
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, key);
    }

    public async Task TouchLastUsedAsync(Guid id, DateTime lastUsedAtUtc, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE api_keys SET last_used_at = @LastUsedAtUtc WHERE id = @Id;";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { Id = id, LastUsedAtUtc = lastUsedAtUtc });
    }

    public async Task<bool> SetCanWriteAsync(Guid id, bool canWrite, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE api_keys
            SET can_write = @CanWrite
            WHERE id = @Id AND revoked_at IS NULL;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { Id = id, CanWrite = canWrite }) > 0;
    }

    public async Task<bool> RevokeAsync(Guid id, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE api_keys
            SET revoked_at = @RevokedAtUtc
            WHERE id = @Id AND revoked_at IS NULL;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { Id = id, RevokedAtUtc = revokedAtUtc }) > 0;
    }
}
