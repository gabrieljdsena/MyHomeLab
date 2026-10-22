using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MyHomeLab.Api.Auth;

namespace MyHomeLab.Api.Middleware;

public sealed class OriginValidationMiddleware(
    RequestDelegate next,
    IConfiguration configuration,
    ILogger<OriginValidationMiddleware> logger)
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "OPTIONS", "TRACE",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (SafeMethods.Contains(context.Request.Method) || OriginPolicy.IsAllowed(context, configuration))
        {
            await next(context);
            return;
        }

        logger.LogWarning(
            "Rejected cross-site {Method} {Path} from origin '{Origin}'",
            context.Request.Method,
            context.Request.Path,
            context.Request.Headers.Origin.ToString());

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = "Cross-site request rejected.",
            Type = "https://httpstatuses.com/403",
        }));
    }
}
