using System.Text;
using Microsoft.Extensions.Configuration;

namespace MyHomeLab.Infrastructure.Auth;

public sealed class AuthOptions
{
    public string JwtSecret { get; init; } = string.Empty;
    public string Issuer { get; init; } = "myhomelab";
    public string Audience { get; init; } = "myhomelab";
    public int SessionDays { get; init; } = 30;
    public int TouchIntervalMinutes { get; init; } = 5;
    public int ReissueAfterHours { get; init; } = 24;

    // Loaded eagerly instead of through IOptions so a missing or weak signing
    // secret fails application startup rather than the first login attempt.
    public static AuthOptions Load(IConfiguration configuration)
    {
        var options = new AuthOptions
        {
            JwtSecret = configuration["Auth:JwtSecret"] ?? string.Empty,
            Issuer = configuration["Auth:Issuer"] ?? "myhomelab",
            Audience = configuration["Auth:Audience"] ?? "myhomelab",
            SessionDays = ParsePositiveInt(configuration["Auth:SessionDays"], 30),
            TouchIntervalMinutes = ParsePositiveInt(configuration["Auth:TouchIntervalMinutes"], 5),
            ReissueAfterHours = ParsePositiveInt(configuration["Auth:ReissueAfterHours"], 24),
        };

        if (Encoding.UTF8.GetByteCount(options.JwtSecret) < 32)
        {
            throw new InvalidOperationException(
                "Auth:JwtSecret is missing or shorter than 32 bytes. " +
                "Set the Auth__JwtSecret environment variable (scripts/dev.ps1 and scripts/install-service.ps1 generate one automatically).");
        }

        return options;
    }

    private static int ParsePositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
