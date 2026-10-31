namespace MyHomeLab.Application.Dtos;

public record PowerRequest(string Action, int? DelaySeconds = null);

public record PowerResponse(string Action, bool Accepted, DateTime RequestedAtUtc, string? Detail);
