using MyHomeLab.Application.Abstractions;

namespace MyHomeLab.Api.Services;

public sealed class LanDiscoveryBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<LanDiscoveryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Machines:Enabled", true)
            || !configuration.GetValue("Machines:DiscoveryEnabled", true))
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(
            Math.Max(60, configuration.GetValue("Machines:DiscoveryIntervalSeconds", 300)));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var discovery = scope.ServiceProvider.GetRequiredService<ILanDiscoveryService>();
                var result = await discovery.ScanAsync(stoppingToken);
                logger.LogInformation(
                    "LAN discovery pass finished: {Devices} devices on {Subnets}.",
                    result.Devices.Count, string.Join(", ", result.Subnets));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "LAN discovery pass failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
