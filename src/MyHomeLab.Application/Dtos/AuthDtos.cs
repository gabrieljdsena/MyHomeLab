namespace MyHomeLab.Application.Dtos;

public record AuthStatusDto(bool NeedsSetup);

public record AuthUserDto(Guid Id, string Username);

public record LoginRequest(string Username, string Password);

public record SetupRequest(string Username, string Password);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record AuthClientInfo(string? UserAgent, string? IpAddress);

public record AuthLoginResult(AuthUserDto User, string Token, DateTime ExpiresAtUtc);

public record SessionValidationResult(bool IsValid, string? RenewedToken, DateTime? RenewedTokenExpiresAtUtc);
