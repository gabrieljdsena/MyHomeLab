using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Application.Abstractions;

public record LogQuery(string? Search = null, string? Application = null, int Page = 1, int PageSize = 50);

public interface ILogRepository
{
    Task<(IReadOnlyList<LogEntry> Items, int TotalCount)> GetPagedAsync(LogQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetApplicationsAsync(CancellationToken cancellationToken);
    Task<LogEntry?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
