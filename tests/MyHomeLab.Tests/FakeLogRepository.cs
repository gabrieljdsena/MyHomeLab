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

    public Task<(IReadOnlyList<LogEntry> Items, int TotalCount)> GetPagedAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = _store.Where(e =>
            (string.IsNullOrEmpty(query.Application) || e.Application == query.Application) &&
            (string.IsNullOrEmpty(query.Search) ||
             e.Application.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
             e.Log.Contains(query.Search, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(e => e.Id)
            .ToArray();

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 50 : query.PageSize;
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();

        return Task.FromResult<(IReadOnlyList<LogEntry>, int)>((items, filtered.Length));
    }

    public Task<IReadOnlyList<string>> GetApplicationsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> applications = _store
            .Select(e => e.Application)
            .Where(a => !string.IsNullOrEmpty(a))
            .Distinct()
            .OrderBy(a => a)
            .ToArray();
        return Task.FromResult(applications);
    }

    public Task<LogEntry?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.FirstOrDefault(e => e.Id == id));
}
