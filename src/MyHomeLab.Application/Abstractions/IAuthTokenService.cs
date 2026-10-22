namespace MyHomeLab.Application.Abstractions;

public record AuthTokenClaims(Guid UserId, Guid SessionId, DateTime IssuedAtUtc, DateTime ExpiresAtUtc);

public interface IAuthTokenService
{
    string CreateToken(AuthTokenClaims claims);

    Task<AuthTokenClaims?> ValidateAsync(string token, CancellationToken cancellationToken = default);
}
