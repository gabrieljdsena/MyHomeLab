using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Errors;

namespace MyHomeLab.Application.Services;

public sealed class AuthService(
    IAuthRepository repository,
    IPasswordHasher passwordHasher,
    IAuthTokenService tokenService,
    AuthPolicy policy,
    TimeProvider timeProvider)
{
    private const int MinUsernameLength = 3;
    private const int MaxUsernameLength = 32;
    private const int MinPasswordLength = 12;
    private const int MaxPasswordLength = 256;
    private const int MaxUserAgentLength = 512;
    private const int MaxIpLength = 64;

    // Verifying against a well-formed dummy hash keeps the "unknown user" path
    // timing-close to the "wrong password" path.
    private static readonly string DummyHash =
        $"pbkdf2-sha256$600000${Convert.ToBase64String(new byte[16])}${Convert.ToBase64String(new byte[32])}";

    public async Task<AuthStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var anyUsers = await repository.AnyUsersAsync(cancellationToken);
        return new AuthStatusDto(!anyUsers);
    }

    public async Task<AuthLoginResult> SetupAsync(SetupRequest request, AuthClientInfo client, CancellationToken cancellationToken)
    {
        var username = NormalizeUsername(request.Username);
        ValidatePassword(request.Password, username);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = new AuthUserRecord(Guid.NewGuid(), username, passwordHasher.Hash(request.Password));
        if (!await repository.TryCreateUserAsync(user, cancellationToken))
        {
            throw new AuthConflictException("An account already exists. Sign in instead.");
        }

        return await CreateLoginResultAsync(user, client, now, cancellationToken);
    }

    public async Task<AuthLoginResult> LoginAsync(LoginRequest request, AuthClientInfo client, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var username = (request.Username ?? string.Empty).Trim().ToLowerInvariant();
        var password = request.Password ?? string.Empty;
        // Hashing happens before any DB work, so an unbounded password would
        // burn 600k PBKDF2 iterations per request on attacker-chosen input.
        if (password.Length > MaxPasswordLength)
        {
            throw new InvalidCredentialsException("Invalid username or password.");
        }

        var user = username.Length == 0 ? null : await repository.FindUserByUsernameAsync(username, cancellationToken);

        if (user is null)
        {
            passwordHasher.Verify(password, DummyHash);
            throw new InvalidCredentialsException("Invalid username or password.");
        }

        if (!passwordHasher.Verify(password, user.PasswordHash))
        {
            throw new InvalidCredentialsException("Invalid username or password.");
        }

        await repository.DeleteExpiredSessionsAsync(now, cancellationToken);
        return await CreateLoginResultAsync(user, client, now, cancellationToken);
    }

    public async Task<SessionValidationResult> ValidateSessionAsync(
        Guid sessionId,
        Guid userId,
        DateTime tokenIssuedAtUtc,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var session = await repository.FindSessionAsync(sessionId, cancellationToken);
        if (session is null || session.UserId != userId || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= now)
        {
            return new SessionValidationResult(false, null, null);
        }

        var extendedExpiry = now + policy.SessionLifetime;
        if (now - session.LastSeenAtUtc >= policy.TouchInterval)
        {
            await repository.TouchSessionAsync(sessionId, now, extendedExpiry, cancellationToken);
        }

        if (now - tokenIssuedAtUtc < policy.ReissueInterval)
        {
            return new SessionValidationResult(true, null, null);
        }

        var renewedToken = tokenService.CreateToken(new AuthTokenClaims(session.UserId, sessionId, now, extendedExpiry));
        return new SessionValidationResult(true, renewedToken, extendedExpiry);
    }

    public Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken) =>
        repository.RevokeSessionAsync(sessionId, cancellationToken);

    public Task<int> LogoutAllAsync(Guid userId, CancellationToken cancellationToken) =>
        repository.RevokeUserSessionsAsync(userId, null, cancellationToken);

    public async Task<AuthUserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await repository.FindUserByIdAsync(userId, cancellationToken)
            ?? throw new InvalidCredentialsException("The account no longer exists.");
        return new AuthUserDto(user.Id, user.Username);
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        Guid currentSessionId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await repository.FindUserByIdAsync(userId, cancellationToken)
            ?? throw new InvalidCredentialsException("The account no longer exists.");

        var currentPassword = request.CurrentPassword ?? string.Empty;
        if (currentPassword.Length > MaxPasswordLength
            || !passwordHasher.Verify(currentPassword, user.PasswordHash))
        {
            throw new ValidationException("The current password is incorrect.");
        }

        ValidatePassword(request.NewPassword, user.Username);
        await repository.UpdatePasswordHashAsync(userId, passwordHasher.Hash(request.NewPassword), cancellationToken);
        await repository.RevokeUserSessionsAsync(userId, currentSessionId, cancellationToken);
    }

    private async Task<AuthLoginResult> CreateLoginResultAsync(
        AuthUserRecord user,
        AuthClientInfo client,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        var expiresAt = now + policy.SessionLifetime;
        await repository.CreateSessionAsync(
            new AuthSessionRecord(
                sessionId,
                user.Id,
                now,
                now,
                expiresAt,
                null,
                Truncate(client.UserAgent, MaxUserAgentLength),
                Truncate(client.IpAddress, MaxIpLength)),
            cancellationToken);

        var token = tokenService.CreateToken(new AuthTokenClaims(user.Id, sessionId, now, expiresAt));
        return new AuthLoginResult(new AuthUserDto(user.Id, user.Username), token, expiresAt);
    }

    private static string NormalizeUsername(string? raw)
    {
        var username = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (username.Length is < MinUsernameLength or > MaxUsernameLength || !username.All(IsUsernameChar))
        {
            throw new ValidationException("Username must be 3-32 characters using letters, digits, dot, dash or underscore.");
        }

        return username;
    }

    private static bool IsUsernameChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_';

    private static void ValidatePassword(string? password, string username)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
        {
            throw new ValidationException($"Password must be between {MinPasswordLength} and {MaxPasswordLength} characters.");
        }

        if (string.Equals(password, username, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Password must not be the same as the username.");
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
