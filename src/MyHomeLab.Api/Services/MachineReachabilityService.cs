using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Services;
using MyHomeLab.Domain;

namespace MyHomeLab.Api.Services;

public sealed class MachineReachabilityService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<MachineReachabilityService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Machines:Enabled", true))
        {
            return;
        }

        var interval = TimeSpan.FromSeconds(
            Math.Max(5, configuration.GetValue("Machines:ProbeIntervalSeconds", (int)TickInterval.TotalSeconds)));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProbeAllAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Machine reachability pass failed.");
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

    private async Task ProbeAllAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var machineService = scope.ServiceProvider.GetRequiredService<MachineService>();
        var probe = scope.ServiceProvider.GetRequiredService<IMachineReachabilityProbe>();

        var machines = await machineService.GetAllAsync(new MachineQuery(EnabledOnly: true), cancellationToken);
        if (machines.Count == 0)
        {
            return;
        }

        await Task.WhenAll(machines.Select(machine => ProbeOneAsync(machineService, probe, machine.Id, machine.Hostname, cancellationToken)));
    }

    private async Task ProbeOneAsync(MachineService machineService, IMachineReachabilityProbe probe, Guid id, string hostname, CancellationToken cancellationToken)
    {
        try
        {
            var result = await probe.ProbeAsync(hostname, cancellationToken);
            await machineService.RecordReachabilityAsync(
                MachineId.From(id), result, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Warning, not Debug: a probe that keeps failing looks identical to a healthy machine
            // that is simply offline, which is exactly the kind of bug that hides for a long time.
            logger.LogWarning(ex, "Reachability probe for {Hostname} failed.", hostname);
        }
    }
}
