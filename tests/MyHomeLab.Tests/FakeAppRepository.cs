using MyHomeLab.Application.Abstractions;
using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Tests;

public sealed class FakeAppRepository : IAppRepository
{
    private readonly Dictionary<AppId, App> _store = [];

    public Task<IReadOnlyList<App>> GetAllAsync(AppQuery query, CancellationToken cancellationToken = default)
    {
        var apps = _store.Values.Where(a =>
            (string.IsNullOrEmpty(query.Search) || a.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(query.Category) || a.Category.Equals(query.Category, StringComparison.OrdinalIgnoreCase)) &&
            (query.EnabledOnly is null || a.IsEnabled == query.EnabledOnly)).ToArray();

        return Task.FromResult<IReadOnlyList<App>>(apps);
    }

    public Task<App?> GetByIdAsync(AppId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<App?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var app = _store.Values.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(app);
    }

    public Task AddAsync(App app, CancellationToken cancellationToken = default)
    {
        if (_store.Values.Any(a => a.Name.Equals(app.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("duplicate");
        }

        _store[app.Id] = app;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(App app, CancellationToken cancellationToken = default)
    {
        _store[app.Id] = app;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AppId id, CancellationToken cancellationToken = default)
    {
        _store.Remove(id);
        return Task.CompletedTask;
    }

    public Task UpdateHealthAsync(App app, CancellationToken cancellationToken = default)
    {
        _store[app.Id] = app;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = _store.Values.Select(a => a.Category).Distinct().ToArray();
        return Task.FromResult<IReadOnlyList<string>>(categories);
    }
}