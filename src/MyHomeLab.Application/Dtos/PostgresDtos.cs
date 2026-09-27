namespace MyHomeLab.Application.Dtos;

/// <summary>
/// A Postgres snapshot with the cumulative counters already turned into rates. Produced by
/// <c>PostgresMetricsService</c>, never straight from the database.
/// </summary>
public record PostgresMetricsDto(
    string Version,
    string DatabaseName,
    long UptimeSeconds,
    long SizeBytes,
    int ConnectionsUsed,
    int ConnectionsMax,
    int ConnectionsActive,
    int ConnectionsIdle,
    int LockWaiters,
    double LongestQuerySeconds,
    double TransactionsPerSecond,
    long TransactionsTotal,
    long RollbacksTotal,
    long Deadlocks,
    double CacheHitRatio,
    double TempBytesPerSecond,
    double WalBytesPerSecond,
    long CheckpointsTotal,
    DateTime SampledAtUtc);
