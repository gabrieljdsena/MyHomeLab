namespace MyHomeLab.Application.Dtos;

public record AppHealthPointDto(
    string Status,
    int? LatencyMs,
    DateTime CheckedAtUtc);

public record HealthHistoryDto(
    IReadOnlyList<AppHealthPointDto> Points,
    HealthUptimeDto Uptime);

public record HealthUptimeDto(
    double UptimePercent,
    int TotalChecks,
    int UpCount,
    int DownCount,
    double? AverageLatencyMs,
    int? MinLatencyMs,
    int? MaxLatencyMs);
