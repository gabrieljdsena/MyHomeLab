using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace MyHomeLab.Infrastructure.Auth;

public static class JwtValidation
{
    public static TokenValidationParameters BuildValidationParameters(AuthOptions options) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSecret)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = "sub",
    };
}
