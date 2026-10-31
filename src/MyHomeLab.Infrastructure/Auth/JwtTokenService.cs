using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MyHomeLab.Application.Abstractions;

namespace MyHomeLab.Infrastructure.Auth;

internal sealed class JwtTokenService : IAuthTokenService
{
    private readonly AuthOptions _options;
    private readonly TokenValidationParameters _validationParameters;

    public JwtTokenService(AuthOptions options)
    {
        _options = options;
        _validationParameters = JwtValidation.BuildValidationParameters(options);
    }

    public string CreateToken(AuthTokenClaims claims)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = claims.IssuedAtUtc,
            NotBefore = claims.IssuedAtUtc,
            Expires = claims.ExpiresAtUtc,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = claims.UserId.ToString(),
                ["sid"] = claims.SessionId.ToString(),
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSecret)),
                SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public async Task<AuthTokenClaims?> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, _validationParameters);
            if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt)
            {
                return null;
            }

            if (!Guid.TryParse(jwt.Subject, out var userId))
            {
                return null;
            }

            if (!jwt.TryGetClaim("sid", out var sidClaim) || !Guid.TryParse(sidClaim.Value, out var sessionId))
            {
                return null;
            }

            return new AuthTokenClaims(userId, sessionId, jwt.ValidFrom, jwt.ValidTo);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
