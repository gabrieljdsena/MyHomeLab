using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Api.Services;

/// <summary>
/// Turns the cumulative <c>pg_stat_*</c> counters into a snapshot the UI can show directly.
/// The <c>pg_stat_*</c> views are monotonic counters that reset when Postgres restarts or when
/// <c>pg_stat_reset()</c> runs, so rates are derived by differencing the previous read against
/// the current one and a backwards counter invalidates the whole window rather than reporting a
/// meaningless spike.
/// </summary>
public sealed class PostgresMetricsService(IPostgresMetricsReader reader)
{
    private readonly Lock _gate = new();
    private PostgresCounters? _previous;

    public async Task<PostgresMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var current = await reader.ReadAsync(cancellationToken);

        lock (_gate)
        {
            var dto = Project(current, _previous);
            _previous = current;
            return dto;
        }
    }

    internal static PostgresMetricsDto Project(PostgresCounters current, PostgresCounters? previous)
    {
        var elapsed = previous is null ? 0 : (current.SampledAtUtc - previous.SampledAtUtc).TotalSeconds;
        var usable = previous is not null && elapsed > 0 && !ResetDetected(current, previous);

        long Delta(long now, long before) => usable && now >= before ? now - before : 0;

        var transactions = current.TransactionsCommitted + current.TransactionsRolledBack;
        var previousTransactions =
            previous is null ? 0 : previous.TransactionsCommitted + previous.TransactionsRolledBack;

        var totalBlocks = current.BlocksRead + current.BlocksHit;

        return new PostgresMetricsDto(
            current.Version,
            current.DatabaseName,
            current.UptimeSeconds,
            current.SizeBytes,
            current.ConnectionsUsed,
            current.ConnectionsMax,
            current.ConnectionsActive,
            current.ConnectionsIdle,
            current.LockWaiters,
            current.LongestQuerySeconds,
            Round(RatePerSecond(transactions, previousTransactions, elapsed)),
            transactions,
            current.TransactionsRolledBack,
            current.Deadlocks,
            totalBlocks > 0 ? Math.Round((double)current.BlocksHit / totalBlocks, 4) : 0,
            Round(RatePerSecond(Delta(current.TempBytes, previous?.TempBytes ?? 0), 0, elapsed)),
            Round(RatePerSecond(Delta(current.WalBytes, previous?.WalBytes ?? 0), 0, elapsed)),
            current.CheckpointsDone,
            current.SampledAtUtc);
    }

    private static bool ResetDetected(PostgresCounters current, PostgresCounters previous) =>
        current.TransactionsCommitted < previous.TransactionsCommitted
        || current.TransactionsRolledBack < previous.TransactionsRolledBack
        || current.BlocksRead < previous.BlocksRead
        || current.BlocksHit < previous.BlocksHit
        || current.TempBytes < previous.TempBytes
        || current.WalBytes < previous.WalBytes
        || current.CheckpointsDone < previous.CheckpointsDone;

    private static double RatePerSecond(long delta, long previous, double elapsedSeconds) =>
        elapsedSeconds <= 0 || delta < previous
            ? 0
            : (delta - previous) / elapsedSeconds;

    private static double Round(double value) => value < 0.0001 ? 0 : Math.Round(value, 2);
}
