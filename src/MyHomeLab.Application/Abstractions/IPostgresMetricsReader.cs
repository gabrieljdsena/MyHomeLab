using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Application.Abstractions;

public interface IPostgresMetricsReader
{
    /// <summary>
    /// Reads the cumulative server/database counters in a single round trip. Implementations
    /// must not throw for a missing or restricted stats view — they should return the counters
    /// they can read and let the caller decide what is missing.
    /// </summary>
    Task<PostgresCounters> ReadAsync(CancellationToken cancellationToken = default);
}
