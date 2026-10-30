using MyHomeLab.Application.Abstractions;

namespace MyHomeLab.Tests;

public sealed class FakeApiKeyRepository : IApiKeyRepository
{
    public List<ApiKeyRecord> Keys { get; } = [];

    public int TouchCalls { get; private set; }

    public Task<IReadOnlyList<ApiKeyRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ApiKeyRecord>>(
            Keys.OrderByDescending(key => key.CreatedAtUtc).ToArray());

    public Task<ApiKeyRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Keys.FirstOrDefault(key => key.Id == id));

    public Task<ApiKeyRecord?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(Keys.FirstOrDefault(key => key.TokenHash == tokenHash));

    public Task AddAsync(ApiKeyRecord key, CancellationToken cancellationToken = default)
    {
        Keys.Add(key);
        return Task.CompletedTask;
    }

    public Task TouchLastUsedAsync(Guid id, DateTime lastUsedAtUtc, CancellationToken cancellationToken = default)
    {
        TouchCalls++;
        var index = Keys.FindIndex(key => key.Id == id);
        if (index >= 0)
        {
            Keys[index] = Keys[index] with { LastUsedAtUtc = lastUsedAtUtc };
        }

        return Task.CompletedTask;
    }

    public Task<bool> SetCanWriteAsync(Guid id, bool canWrite, CancellationToken cancellationToken = default)
    {
        var index = Keys.FindIndex(key => key.Id == id && key.RevokedAtUtc is null);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        Keys[index] = Keys[index] with { CanWrite = canWrite };
        return Task.FromResult(true);
    }

    public Task<bool> RevokeAsync(Guid id, DateTime revokedAtUtc, CancellationToken cancellationToken = default)
    {
        var index = Keys.FindIndex(key => key.Id == id && key.RevokedAtUtc is null);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        Keys[index] = Keys[index] with { RevokedAtUtc = revokedAtUtc };
        return Task.FromResult(true);
    }
}
