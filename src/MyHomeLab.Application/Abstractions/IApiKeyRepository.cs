namespace MyHomeLab.Application.Abstractions;

public record ApiKeyRecord(
    Guid Id,
    string Name,
    string Prefix,
    string TokenHash,
    DateTime CreatedAtUtc,
    DateTime? LastUsedAtUtc,
    DateTime? RevokedAtUtc,
    bool CanWrite = false);

public interface IApiKeyRepository
{
    Task<IReadOnlyList<ApiKeyRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ApiKeyRecord?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApiKeyRecord?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddAsync(ApiKeyRecord key, CancellationToken cancellationToken = default);

    Task TouchLastUsedAsync(Guid id, DateTime lastUsedAtUtc, CancellationToken cancellationToken = default);

    Task<bool> SetCanWriteAsync(Guid id, bool canWrite, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(Guid id, DateTime revokedAtUtc, CancellationToken cancellationToken = default);
}
