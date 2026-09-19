namespace MyHomeLab.Application.Dtos;

public record PowerRequest(string Action);

public record PowerResponse(string Action, bool Accepted, DateTime RequestedAtUtc, string? Detail);
