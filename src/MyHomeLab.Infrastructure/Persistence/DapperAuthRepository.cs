using Dapper;
using MyHomeLab.Application.Abstractions;
using Npgsql;

namespace MyHomeLab.Infrastructure.Persistence;

internal sealed class DapperAuthRepository(NpgsqlDataSource dataSource) : IAuthRepository
{
    private const long SetupAdvisoryLockKey = 712907001;

    public async Task<bool> AnyUsersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM users);");
    }

    public async Task<AuthUserRecord?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS "Id", username AS "Username", password_hash AS "PasswordHash"
            FROM users
            WHERE username = @Username;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AuthUserRecord>(sql, new { Username = username });
    }

    public async Task<AuthUserRecord?> FindUserByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS "Id", username AS "Username", password_hash AS "PasswordHash"
            FROM users
            WHERE id = @Id;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AuthUserRecord>(sql, new { Id = id });
    }

    public async Task<bool> TryCreateUserAsync(AuthUserRecord user, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var locked = await connection.ExecuteScalarAsync<bool>(
            "SELECT pg_try_advisory_xact_lock(@Key);",
            new { Key = SetupAdvisoryLockKey },
            transaction);
        if (!locked)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var exists = await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM users);",
            transaction: transaction);
        if (exists)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await connection.ExecuteAsync(
            "INSERT INTO users (id, username, password_hash) VALUES (@Id, @Username, @PasswordHash);",
            user,
            transaction);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task UpdatePasswordHashAsync(Guid userId, string passwordHash, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE users
            SET password_hash = @PasswordHash, updated_at = now()
            WHERE id = @UserId;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { UserId = userId, PasswordHash = passwordHash });
    }

    public async Task CreateSessionAsync(AuthSessionRecord session, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO auth_sessions (id, user_id, created_at, last_seen_at, expires_at, user_agent, created_ip)
            VALUES (@Id, @UserId, @CreatedAtUtc, @LastSeenAtUtc, @ExpiresAtUtc, @UserAgent, @CreatedIp);
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, session);
    }

    public async Task<AuthSessionRecord?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id           AS "Id",
                   user_id      AS "UserId",
                   created_at   AS "CreatedAtUtc",
                   last_seen_at AS "LastSeenAtUtc",
                   expires_at   AS "ExpiresAtUtc",
                   revoked_at   AS "RevokedAtUtc",
                   user_agent   AS "UserAgent",
                   created_ip   AS "CreatedIp"
            FROM auth_sessions
            WHERE id = @SessionId;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<AuthSessionRecord>(sql, new { SessionId = sessionId });
    }

    public async Task TouchSessionAsync(Guid sessionId, DateTime lastSeenAtUtc, DateTime expiresAtUtc, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE auth_sessions
            SET last_seen_at = @LastSeenAtUtc, expires_at = @ExpiresAtUtc
            WHERE id = @SessionId AND revoked_at IS NULL;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { SessionId = sessionId, LastSeenAtUtc = lastSeenAtUtc, ExpiresAtUtc = expiresAtUtc });
    }

    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE auth_sessions
            SET revoked_at = now()
            WHERE id = @SessionId AND revoked_at IS NULL;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(sql, new { SessionId = sessionId });
    }

    public async Task<int> RevokeUserSessionsAsync(Guid userId, Guid? exceptSessionId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE auth_sessions
            SET revoked_at = now()
            WHERE user_id = @UserId
              AND revoked_at IS NULL
              AND (@ExceptSessionId IS NULL OR id <> @ExceptSessionId);
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { UserId = userId, ExceptSessionId = exceptSessionId });
    }

    public async Task<int> DeleteExpiredSessionsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM auth_sessions WHERE expires_at < @NowUtc;";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(sql, new { NowUtc = nowUtc });
    }
}
