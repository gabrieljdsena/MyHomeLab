namespace MyHomeLab.Domain.Entities;

public sealed class AppHealthSample
{
    public long Id { get; }
    public AppId AppId { get; }
    public AppHealthStatus Status { get; }
    public int? LatencyMs { get; }
    public DateTime CheckedAtUtc { get; }

    public AppHealthSample(long id, AppId appId, AppHealthStatus status, int? latencyMs, DateTime checkedAtUtc)
    {
        if (id < 0)
        {
            throw new DomainException("Health sample id cannot be negative.");
        }

        if (latencyMs is < 0)
        {
            throw new DomainException("Latency cannot be negative.");
        }

        Id = id;
        AppId = appId;
        Status = status;
        LatencyMs = latencyMs;
        CheckedAtUtc = checkedAtUtc.Kind == DateTimeKind.Utc ? checkedAtUtc : checkedAtUtc.ToUniversalTime();
    }

    public static AppHealthSample Create(AppId appId, AppHealthStatus status, int? latencyMs, DateTime? checkedAtUtc = null)
    {
        return new AppHealthSample(0, appId, status, latencyMs, checkedAtUtc ?? DateTime.UtcNow);
    }
}
