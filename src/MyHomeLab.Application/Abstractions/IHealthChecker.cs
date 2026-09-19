using MyHomeLab.Domain;

namespace MyHomeLab.Application.Abstractions;

public record HealthCheckOutcome(AppHealthStatus Status, int? LatencyMs);

public interface IHealthChecker
{
    Task<HealthCheckOutcome> ProbeAsync(string url, CancellationToken cancellationToken);
}