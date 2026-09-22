using MyHomeLab.Application.Dtos;
using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Application.Mapping;

public static class HealthMapping
{
    public static AppHealthPointDto ToPoint(this AppHealthSample sample) =>
        new(
            sample.Status.ToString().ToLowerInvariant(),
            sample.LatencyMs,
            sample.CheckedAtUtc);

    public static HealthHistoryDto ToHistoryDto(this IReadOnlyList<AppHealthSample> samples)
    {
        if (samples.Count == 0)
        {
            return new HealthHistoryDto([], new HealthUptimeDto(0, 0, 0, 0, null, null, null));
        }

        var upCount = samples.Count(s => s.Status == Domain.AppHealthStatus.Up);
        var downCount = samples.Count(s => s.Status == Domain.AppHealthStatus.Down);
        var total = samples.Count;
        var uptimePercent = total == 0 ? 0 : (double)upCount / total * 100;

        var latencies = samples.Where(s => s.LatencyMs.HasValue).Select(s => s.LatencyMs!.Value).ToArray();
        double? avg = latencies.Length > 0 ? latencies.Average() : null;
        int? min = latencies.Length > 0 ? latencies.Min() : null;
        int? max = latencies.Length > 0 ? latencies.Max() : null;

        var points = samples.Select(s => s.ToPoint()).ToArray();

        return new HealthHistoryDto(
            points,
            new HealthUptimeDto(
                Math.Round(uptimePercent, 1),
                total,
                upCount,
                downCount,
                avg is null ? null : Math.Round(avg.Value, 1),
                min,
                max));
    }
}
