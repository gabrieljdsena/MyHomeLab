namespace MyHomeLab.Application.Dtos;

/// <summary>
/// One raw read of the cumulative <c>pg_stat_*</c> counters. Everything rate-shaped
/// (transactions/sec, WAL/sec) has to be derived by differencing two of these, so the raw
/// totals are kept out of the API DTO and never shown to the user as-is.
/// </summary>
public record PostgresCounters(
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
    long TransactionsCommitted,
    long TransactionsRolledBack,
    long Deadlocks,
    long BlocksRead,
    long BlocksHit,
    long TempBytes,
    long WalBytes,
    long CheckpointsDone,
    DateTime SampledAtUtc);
