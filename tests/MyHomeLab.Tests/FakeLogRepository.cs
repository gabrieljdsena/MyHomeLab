using MyHomeLab.Application.Abstractions;
using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Tests;

public sealed class FakeLogRepository : ILogRepository
{
    private readonly List<LogEntry> _store = [];
    private int _nextId = 1;

    public void Seed(string application, string log)
    {
        _store.Add(LogEntry.Restore(_nextId++, application, log));
    }

    public Task<IReadOnlyList<LogEntry>> GetAllAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        var entries = _store.Where(e =>
            (string.IsNullOrEmpty(query.Application) || e.Application == query.Application) &&
            (string.IsNullOrEmpty(query.Search) ||
             e.Application.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
             e.Log.Contains(query.Search, StringComparison.OrdinalIgnoreCase))).ToArray();

        return Task.FromResult<IReadOnlyList<LogEntry>>(entries);
    }

    public Task<LogEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.FirstOrDefault(e => e.Id == id));
}
