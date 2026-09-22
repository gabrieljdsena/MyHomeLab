using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Application.Abstractions;

public sealed record HealthHistoryQuery(
    AppId AppId,
    DateTime? SinceUtc = null,
    int Limit = 200);

public interface IHealthHistoryRepository
{
    Task AddAsync(AppHealthSample sample, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppHealthSample>> GetRecentAsync(HealthHistoryQuery query, CancellationToken cancellationToken = default);
}
