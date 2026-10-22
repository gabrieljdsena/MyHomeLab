using MyHomeLab.Application.Abstractions;
using MyHomeLab.Infrastructure.Auth;
using Xunit;

namespace MyHomeLab.Tests;

public class JwtTokenServiceTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";

    private static readonly DateTime Now = Truncate(DateTime.UtcNow);

    private static DateTime Truncate(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    private static AuthOptions Options(string secret = Secret) => new()
    {
        JwtSecret = secret,
        Issuer = "myhomelab",
        Audience = "myhomelab",
    };

    [Fact]
    public async Task Create_Then_Validate_Roundtrips_Claims()
    {
        var service = new JwtTokenService(Options());
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var token = service.CreateToken(new AuthTokenClaims(userId, sessionId, Now, Now.AddDays(30)));
        var claims = await service.ValidateAsync(token);

        Assert.NotNull(claims);
        Assert.Equal(userId, claims.UserId);
        Assert.Equal(sessionId, claims.SessionId);
        Assert.Equal(Now, claims.IssuedAtUtc);
        Assert.Equal(Now.AddDays(30), claims.ExpiresAtUtc);
    }

    [Fact]
    public async Task Validate_Tampered_Token_Returns_Null()
    {
        var service = new JwtTokenService(Options());
        var token = service.CreateToken(new AuthTokenClaims(Guid.NewGuid(), Guid.NewGuid(), Now, Now.AddDays(30)));

        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        Assert.Null(await service.ValidateAsync(tampered));
    }

    [Fact]
    public async Task Validate_Expired_Token_Returns_Null()
    {
        var service = new JwtTokenService(Options());
        var token = service.CreateToken(new AuthTokenClaims(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddMinutes(-10),
            Now.AddMinutes(-2)));

        Assert.Null(await service.ValidateAsync(token));
    }

    [Fact]
    public async Task Validate_Wrong_Secret_Returns_Null()
    {
        var issuer = new JwtTokenService(Options());
        var token = issuer.CreateToken(new AuthTokenClaims(Guid.NewGuid(), Guid.NewGuid(), Now, Now.AddDays(30)));

        var validator = new JwtTokenService(Options("abcdef0123456789abcdef0123456789"));

        Assert.Null(await validator.ValidateAsync(token));
    }

    [Fact]
    public async Task Validate_Garbage_Returns_Null()
    {
        var service = new JwtTokenService(Options());

        Assert.Null(await service.ValidateAsync("not.a.jwt"));
    }

    [Fact]
    public async Task Validate_Asymmetric_Signed_Token_Returns_Null()
    {
        var service = new JwtTokenService(Options());
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var descriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Issuer = "myhomelab",
            Audience = "myhomelab",
            IssuedAt = Now,
            NotBefore = Now,
            Expires = Now.AddDays(30),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["sid"] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256),
        };
        var token = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(descriptor);

        Assert.Null(await service.ValidateAsync(token));
    }
}
