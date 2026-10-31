using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MyHomeLab.Application.Services;

namespace MyHomeLab.Api.Auth;

public sealed class ApiTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApiKeyService apiKeys) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiToken";
    public const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValue))
        {
            return AuthenticateResult.NoResult();
        }

        var token = headerValue.ToString().Trim();
        if (token.Length == 0)
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            var key = await apiKeys.ValidateAsync(token, Context.RequestAborted);
            if (key is null)
            {
                Logger.LogWarning(
                    "Rejected invalid API key from {RemoteIp}.",
                    Context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                return AuthenticateResult.Fail("Invalid API key.");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, $"api-key:{key.Id}"),
                new Claim(ClaimTypes.Name, key.Name),
                new Claim("scope", "homelab:read"),
                new Claim("auth_method", "api_token"),
            };
            if (key.CanWrite)
            {
                claims.Add(new Claim("scope", "homelab:write"));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "API key validation failed.");
            return AuthenticateResult.Fail("API key validation failed.");
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // Multiple schemes can challenge one request (for example when a bad
        // X-Api-Key also falls through to the JWT scheme). Only the first one
        // may write the response.
        if (Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";
        return Response.WriteAsync(JsonSerializer.Serialize(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Authentication is required.",
            Type = "https://httpstatuses.com/401",
        }));
    }
}
