using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Application.Abstractions;

public record AppQuery(string? Search = null, string? Category = null, bool? EnabledOnly = null);

public interface IAppRepository
{
    Task<IReadOnlyList<App>> GetAllAsync(AppQuery query, CancellationToken cancellationToken);

    Task<App?> GetByIdAsync(AppId id, CancellationToken cancellationToken);

    Task<App?> GetByNameAsync(string name, CancellationToken cancellationToken);

    Task AddAsync(App app, CancellationToken cancellationToken);

    Task UpdateAsync(App app, CancellationToken cancellationToken);

    Task DeleteAsync(AppId id, CancellationToken cancellationToken);

    Task UpdateHealthAsync(App app, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken);
}