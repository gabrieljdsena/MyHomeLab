namespace MyHomeLab.Api.Auth;

public static class OriginPolicy
{
    public static bool IsAllowed(HttpContext context, IConfiguration configuration)
    {
        var origin = ResolveOrigin(context.Request);
        if (origin is null)
        {
            return true;
        }

        if (MatchesRequestHost(origin, context.Request))
        {
            return true;
        }

        return GetConfiguredOrigins(configuration).Contains(origin, StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveOrigin(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        if (!string.IsNullOrWhiteSpace(origin))
        {
            return Normalize(origin);
        }

        var referer = request.Headers.Referer.ToString();
        if (string.IsNullOrWhiteSpace(referer))
        {
            return null;
        }

        return Uri.TryCreate(referer, UriKind.Absolute, out var uri) ? $"{uri.Scheme}://{uri.Authority}" : null;
    }

    private static bool MatchesRequestHost(string origin, HttpRequest request) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && string.Equals(uri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase)
        // Scheme matters: https://hub and http://hub are different sites
        // (Schemeful Same-Site) and must not vouch for each other.
        && string.Equals(uri.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> GetConfiguredOrigins(IConfiguration configuration) =>
        (configuration["Cors:AllowedOrigins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize);

    private static string Normalize(string origin) => origin.Trim().TrimEnd('/');
}
