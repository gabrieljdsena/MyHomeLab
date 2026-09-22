using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Errors;
using MyHomeLab.Application.Mapping;
using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Application.Services;

public sealed class AppService(
    IAppRepository repository,
    IHealthHistoryRepository? historyRepository = null)
{
    public async Task<IReadOnlyList<AppDetailDto>> GetAllAsync(AppQuery query, CancellationToken cancellationToken = default)
    {
        var apps = await repository.GetAllAsync(query, cancellationToken);
        return apps
            .OrderBy(a => a.Category)
            .ThenBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .Select(a => a.ToDetail())
            .ToArray();
    }

    public async Task<AppDetailDto> GetByIdAsync(AppId id, CancellationToken cancellationToken = default)
    {
        var app = await repository.GetByIdAsync(id, cancellationToken);
        if (app is null)
        {
            throw new AppNotFoundException($"App '{id}' was not found.");
        }

        return app.ToDetail();
    }

    public async Task<AppDetailDto> CreateAsync(CreateAppRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureUniqueNameAsync(request.Name, null, cancellationToken);

        var app = App.Create(
            request.Name,
            request.Url,
            request.Description,
            request.Icon,
            request.Category,
            request.Port,
            request.Tags,
            request.DockerContainer,
            request.HealthCheckEnabled,
            request.HealthCheckIntervalMs,
            request.SortOrder);

        await repository.AddAsync(app, cancellationToken);
        return await GetByIdAsync(app.Id, cancellationToken);
    }

    public async Task<AppDetailDto> UpdateAsync(AppId id, UpdateAppRequest request, CancellationToken cancellationToken = default)
    {
        var app = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppNotFoundException($"App '{id}' was not found.");

        await EnsureUniqueNameAsync(request.Name, id, cancellationToken);

        app.UpdateDetails(
            request.Name,
            request.Url,
            request.Description,
            request.Icon,
            request.Category,
            request.Port,
            request.Tags,
            request.DockerContainer,
            request.HealthCheckEnabled,
            request.HealthCheckIntervalMs,
            request.SortOrder);

        await repository.UpdateAsync(app, cancellationToken);
        return app.ToDetail();
    }

    public async Task<AppDetailDto> PatchAsync(AppId id, PatchAppRequest request, CancellationToken cancellationToken = default)
    {
        var app = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppNotFoundException($"App '{id}' was not found.");

        var name = request.Name ?? app.Name;
        if (!string.Equals(name, app.Name, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureUniqueNameAsync(name, id, cancellationToken);
        }

        app.UpdateDetails(
            name,
            request.Url ?? app.Url,
            request.Description ?? app.Description,
            request.Icon ?? app.Icon,
            request.Category ?? app.Category,
            request.Port ?? app.Port,
            request.Tags ?? app.Tags,
            request.DockerContainer ?? app.DockerContainer,
            request.HealthCheckEnabled ?? app.HealthCheckEnabled,
            request.HealthCheckIntervalMs ?? app.HealthCheckIntervalMs,
            request.SortOrder ?? app.SortOrder);

        if (request.IsEnabled == true)
        {
            app.Enable();
        }
        else if (request.IsEnabled == false)
        {
            app.Disable();
        }

        await repository.UpdateAsync(app, cancellationToken);
        return app.ToDetail();
    }

    public async Task DeleteAsync(AppId id, CancellationToken cancellationToken = default)
    {
        var app = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppNotFoundException($"App '{id}' was not found.");

        await repository.DeleteAsync(app.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var stored = await repository.GetCategoriesAsync(cancellationToken);
        return stored
            .Concat(KnownCategories.All)
            .Select(c => c.Trim().ToLowerInvariant())
            .Where(c => c.Length > 0)
            .Distinct()
            .OrderBy(c => c)
            .ToArray();
    }

    public async Task<HealthResultDto> ProbeAsync(AppId id, IHealthChecker checker, CancellationToken cancellationToken = default)
    {
        var app = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppNotFoundException($"App '{id}' was not found.");

        var outcome = await checker.ProbeAsync(app.Url, cancellationToken);
        var checkedAt = DateTime.UtcNow;
        app.SetHealth(outcome.Status, outcome.LatencyMs, checkedAt);
        await repository.UpdateHealthAsync(app, cancellationToken);

        if (historyRepository is not null)
        {
            try
            {
                var sample = AppHealthSample.Create(app.Id, outcome.Status, outcome.LatencyMs, checkedAt);
                await historyRepository.AddAsync(sample, cancellationToken);
            }
            catch
            {
                // History is best-effort; health status already persisted.
            }
        }

        return new HealthResultDto(outcome.Status.ToString().ToLowerInvariant(), outcome.LatencyMs);
    }

    public async Task<HealthHistoryDto> GetHealthHistoryAsync(AppId id, int hours, int limit, CancellationToken cancellationToken = default)
    {
        var app = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new AppNotFoundException($"App '{id}' was not found.");

        if (historyRepository is null)
        {
            return new HealthHistoryDto([], new HealthUptimeDto(0, 0, 0, 0, null, null, null));
        }

        hours = hours < 1 ? 1 : hours > 720 ? 720 : hours;
        limit = limit < 1 ? 1 : limit > 1000 ? 1000 : limit;

        var sinceUtc = DateTime.UtcNow.AddHours(-hours);
        var samples = await historyRepository.GetRecentAsync(
            new HealthHistoryQuery(app.Id, sinceUtc, limit),
            cancellationToken);

        return samples.ToHistoryDto();
    }

    private async Task EnsureUniqueNameAsync(string name, AppId? excludeId, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByNameAsync(name.Trim(), cancellationToken);
        if (existing is not null && existing.Id != excludeId)
        {
            throw new AppConflictException($"An app named '{name}' already exists.");
        }
    }
}
