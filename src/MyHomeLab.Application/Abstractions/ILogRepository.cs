using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Application.Abstractions;

public record LogQuery(string? Search = null, string? Application = null, int Limit = 200);

public interface ILogRepository
{
    Task<IReadOnlyList<LogEntry>> GetAllAsync(LogQuery query, CancellationToken cancellationToken);
    Task<LogEntry?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
