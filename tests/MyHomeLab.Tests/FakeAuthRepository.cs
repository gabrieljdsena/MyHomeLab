using MyHomeLab.Application.Abstractions;

namespace MyHomeLab.Tests;

public sealed class FakeAuthRepository : IAuthRepository
{
    public List<AuthUserRecord> Users { get; } = [];

    public List<AuthSessionRecord> Sessions { get; } = [];

    public int TouchCalls { get; private set; }

    public int DeleteExpiredCalls { get; private set; }

    public Task<bool> AnyUsersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Count > 0);

    public Task<AuthUserRecord?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Username == username));

    public Task<AuthUserRecord?> FindUserByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<bool> TryCreateUserAsync(AuthUserRecord user, CancellationToken cancellationToken = default)
    {
        if (Users.Count > 0)
        {
            return Task.FromResult(false);
        }

        Users.Add(user);
        return Task.FromResult(true);
    }

    public Task UpdatePasswordHashAsync(Guid userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        var index = Users.FindIndex(u => u.Id == userId);
        if (index >= 0)
        {
            Users[index] = Users[index] with { PasswordHash = passwordHash };
        }

        return Task.CompletedTask;
    }

    public Task CreateSessionAsync(AuthSessionRecord session, CancellationToken cancellationToken = default)
    {
        Sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<AuthSessionRecord?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.Id == sessionId));

    public Task TouchSessionAsync(Guid sessionId, DateTime lastSeenAtUtc, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        TouchCalls++;
        var index = Sessions.FindIndex(s => s.Id == sessionId);
        if (index >= 0)
        {
            Sessions[index] = Sessions[index] with { LastSeenAtUtc = lastSeenAtUtc, ExpiresAtUtc = expiresAtUtc };
        }

        return Task.CompletedTask;
    }

    public Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var index = Sessions.FindIndex(s => s.Id == sessionId);
        if (index >= 0 && Sessions[index].RevokedAtUtc is null)
        {
            Sessions[index] = Sessions[index] with { RevokedAtUtc = DateTime.UtcNow };
        }

        return Task.CompletedTask;
    }

    public Task<int> RevokeUserSessionsAsync(Guid userId, Guid? exceptSessionId, CancellationToken cancellationToken = default)
    {
        var revoked = 0;
        for (var i = 0; i < Sessions.Count; i++)
        {
            var session = Sessions[i];
            if (session.UserId == userId && session.RevokedAtUtc is null && session.Id != exceptSessionId)
            {
                Sessions[i] = session with { RevokedAtUtc = DateTime.UtcNow };
                revoked++;
            }
        }

        return Task.FromResult(revoked);
    }

    public Task<int> DeleteExpiredSessionsAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        DeleteExpiredCalls++;
        return Task.FromResult(Sessions.RemoveAll(s => s.ExpiresAtUtc < nowUtc));
    }
}
