using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyHomeLab.Application.Services;
using MyHomeLab.Api.Middleware;
using MyHomeLab.Infrastructure.Auth;

namespace MyHomeLab.Api.Auth;

public static class AuthSetup
{
    public const string LoginRateLimitPolicy = "auth";

    public static IServiceCollection AddMyHomeLabAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var options = AuthOptions.Load(configuration);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = JwtValidation.BuildValidationParameters(options);
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies[AuthCookie.Name];
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var hasUserId = Guid.TryParse(principal?.FindFirst("sub")?.Value, out var userId);
                        var hasSessionId = Guid.TryParse(principal?.FindFirst("sid")?.Value, out var sessionId);
                        if (!hasUserId || !hasSessionId)
                        {
                            context.Fail("Token is missing required claims.");
                            return;
                        }

                        var authService = context.HttpContext.RequestServices.GetRequiredService<AuthService>();
                        var validation = await authService.ValidateSessionAsync(
                            sessionId,
                            userId,
                            context.SecurityToken.ValidFrom,
                            context.HttpContext.RequestAborted);
                        if (!validation.IsValid)
                        {
                            context.Fail("Session expired or revoked.");
                            return;
                        }

                        if (validation.RenewedToken is not null && validation.RenewedTokenExpiresAtUtc is not null)
                        {
                            AuthCookie.Append(context.HttpContext.Response, validation.RenewedToken, validation.RenewedTokenExpiresAtUtc.Value);
                        }
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Unauthorized",
                            Detail = "Authentication is required.",
                            Type = "https://httpstatuses.com/401",
                        }));
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(LoginRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(15),
                    QueueLimit = 0,
                }));
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = "Too many attempts. Try again later.",
                    Type = "https://httpstatuses.com/429",
                }), cancellationToken);
            };
        });

        return services;
    }

    public static IApplicationBuilder UseMyHomeLabAuth(this IApplicationBuilder app)
    {
        app.UseMiddleware<OriginValidationMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        return app;
    }
}
