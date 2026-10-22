namespace MyHomeLab.Application.Abstractions;

public record AuthUserRecord(Guid Id, string Username, string PasswordHash);

public record AuthSessionRecord(
    Guid Id,
    Guid UserId,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    string? UserAgent,
    string? CreatedIp);

public interface IAuthRepository
{
    Task<bool> AnyUsersAsync(CancellationToken cancellationToken);

    Task<AuthUserRecord?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<AuthUserRecord?> FindUserByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> TryCreateUserAsync(AuthUserRecord user, CancellationToken cancellationToken);

    Task UpdatePasswordHashAsync(Guid userId, string passwordHash, CancellationToken cancellationToken);

    Task CreateSessionAsync(AuthSessionRecord session, CancellationToken cancellationToken);

    Task<AuthSessionRecord?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    Task TouchSessionAsync(Guid sessionId, DateTime lastSeenAtUtc, DateTime expiresAtUtc, CancellationToken cancellationToken);

    Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<int> RevokeUserSessionsAsync(Guid userId, Guid? exceptSessionId, CancellationToken cancellationToken);

    Task<int> DeleteExpiredSessionsAsync(DateTime nowUtc, CancellationToken cancellationToken);
}
