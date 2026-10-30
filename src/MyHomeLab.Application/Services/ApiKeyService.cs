using System.Security.Cryptography;
using System.Text;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Errors;

namespace MyHomeLab.Application.Services;

public sealed class ApiKeyService(IApiKeyRepository repository, TimeProvider timeProvider)
{
    public const int MaxNameLength = 80;

    private const string TokenPrefix = "mhl_";
    private const int TokenBytes = 32;
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<ApiKeyDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var keys = await repository.GetAllAsync(cancellationToken);
        return keys.Select(ToDto).ToArray();
    }

    public async Task<CreateApiKeyResult> CreateAsync(
        string name,
        bool canWrite = false,
        CancellationToken cancellationToken = default)
    {
        var cleanName = NormalizeName(name);
        var token = NewToken();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var record = new ApiKeyRecord(
            Guid.NewGuid(),
            cleanName,
            Prefix(token),
            HashToken(token),
            now,
            null,
            null,
            canWrite);

        await repository.AddAsync(record, cancellationToken);
        return new CreateApiKeyResult(ToDto(record), token);
    }

    public async Task<ApiKeyDto> SetCanWriteAsync(
        Guid id,
        bool canWrite,
        CancellationToken cancellationToken = default)
    {
        var key = await repository.FindByIdAsync(id, cancellationToken)
            ?? throw new ApiKeyNotFoundException($"API key '{id}' was not found.");

        if (key.RevokedAtUtc is not null)
        {
            throw new ValidationException("Revoked keys cannot be changed.");
        }

        if (!await repository.SetCanWriteAsync(id, canWrite, cancellationToken))
        {
            throw new ApiKeyNotFoundException($"API key '{id}' was not found.");
        }

        return ToDto(key with { CanWrite = canWrite });
    }

    public async Task<ApiKeyDto> RevokeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = await repository.FindByIdAsync(id, cancellationToken)
            ?? throw new ApiKeyNotFoundException($"API key '{id}' was not found.");

        if (key.RevokedAtUtc is null)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (!await repository.RevokeAsync(id, now, cancellationToken))
            {
                throw new ApiKeyNotFoundException($"API key '{id}' was not found.");
            }

            key = key with { RevokedAtUtc = now };
        }

        return ToDto(key);
    }

    public async Task<ApiKeyRecord?> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        var cleanToken = (token ?? string.Empty).Trim();
        if (!cleanToken.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var key = await repository.FindByHashAsync(HashToken(cleanToken), cancellationToken);
        if (key is null || key.RevokedAtUtc is not null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (key.LastUsedAtUtc is null || now - key.LastUsedAtUtc.Value >= TouchInterval)
        {
            try
            {
                await repository.TouchLastUsedAsync(key.Id, now, cancellationToken);
            }
            catch
            {
                // Last-used telemetry must never make a valid key fail auth.
            }
        }

        return key;
    }

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string NewToken() =>
        TokenPrefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenBytes)).ToLowerInvariant();

    private static string Prefix(string token) => token[..Math.Min(12, token.Length)];

    private static string NormalizeName(string? name)
    {
        var clean = (name ?? string.Empty).Trim();
        if (clean.Length is < 1 or > MaxNameLength)
        {
            throw new ValidationException($"Name must be between 1 and {MaxNameLength} characters.");
        }

        return clean;
    }

    private static ApiKeyDto ToDto(ApiKeyRecord key) =>
        new(key.Id, key.Name, key.Prefix, key.CreatedAtUtc, key.LastUsedAtUtc, key.RevokedAtUtc, key.CanWrite);
}
