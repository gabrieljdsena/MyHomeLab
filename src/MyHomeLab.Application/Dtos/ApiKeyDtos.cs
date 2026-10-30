namespace MyHomeLab.Application.Dtos;

public record ApiKeyDto(
    Guid Id,
    string Name,
    string Prefix,
    DateTime CreatedAtUtc,
    DateTime? LastUsedAtUtc,
    DateTime? RevokedAtUtc,
    bool CanWrite);

public record CreateApiKeyRequest(string Name, bool CanWrite = false);

public record UpdateApiKeyRequest(bool CanWrite);

public record CreateApiKeyResult(ApiKeyDto ApiKey, string Token);
