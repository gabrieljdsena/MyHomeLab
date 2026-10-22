using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyHomeLab.Api.Auth;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Services;

namespace MyHomeLab.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService authService) : ControllerBase
{
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await authService.GetStatusAsync(cancellationToken));
    }

    [HttpPost("setup")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSetup.LoginRateLimitPolicy)]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Setup(SetupRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.SetupAsync(request, ClientInfo(), cancellationToken);
        AuthCookie.Append(Response, result.Token, result.ExpiresAtUtc);
        return Ok(result.User);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSetup.LoginRateLimitPolicy)]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, ClientInfo(), cancellationToken);
        AuthCookie.Append(Response, result.Token, result.ExpiresAtUtc);
        return Ok(result.User);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (TryGetSessionId(out var sessionId))
        {
            await authService.LogoutAsync(sessionId, cancellationToken);
        }

        AuthCookie.Delete(Response);
        return NoContent();
    }

    [HttpPost("logout-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        if (TryGetUserId(out var userId))
        {
            await authService.LogoutAllAsync(userId, cancellationToken);
        }

        AuthCookie.Delete(Response);
        return NoContent();
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await authService.GetCurrentUserAsync(userId, cancellationToken));
    }

    [HttpPost("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId) || !TryGetSessionId(out var sessionId))
        {
            return Unauthorized();
        }

        await authService.ChangePasswordAsync(userId, sessionId, request, cancellationToken);
        return NoContent();
    }

    private AuthClientInfo ClientInfo() => new(
        Request.Headers.UserAgent.ToString(),
        HttpContext.Connection.RemoteIpAddress?.ToString());

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue("sub"), out userId);

    private bool TryGetSessionId(out Guid sessionId) =>
        Guid.TryParse(User.FindFirstValue("sid"), out sessionId);
}
