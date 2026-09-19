using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Services;
using MyHomeLab.Domain;

namespace MyHomeLab.Api.Services;

public sealed class HealthCheckBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<HealthCheckBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);
    private readonly Dictionary<Guid, DateTime> _lastRuns = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckDueAppsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Health check pass failed.");
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CheckDueAppsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var appService = scope.ServiceProvider.GetRequiredService<AppService>();
        var healthChecker = scope.ServiceProvider.GetRequiredService<IHealthChecker>();

        var apps = await appService.GetAllAsync(new AppQuery(), cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var app in apps.Where(a => a.IsEnabled && a.HealthCheckEnabled))
        {
            if (_lastRuns.TryGetValue(app.Id, out var lastRun) &&
                now - lastRun < TimeSpan.FromMilliseconds(app.HealthCheckIntervalMs))
            {
                continue;
            }

            try
            {
                await appService.ProbeAsync(AppId.From(app.Id), healthChecker, cancellationToken);
                _lastRuns[app.Id] = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Health probe for app {AppName} failed.", app.Name);
            }
        }
    }
}