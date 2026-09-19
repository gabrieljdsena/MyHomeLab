using System.Runtime.Versioning;
using Microsoft.Management.Infrastructure;
using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Api.Services;

public sealed class StorageSmartService : IDisposable
{
    private readonly ILogger<StorageSmartService> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private volatile Snapshot _snapshot = new([], []);

    private sealed record Snapshot(
        IReadOnlyList<TemperatureReadingDto> DiskTemperatures,
        IReadOnlyList<StorageHealthReadingDto> StorageHealth);

    public StorageSmartService(ILogger<StorageSmartService> logger)
    {
        _logger = logger;
        _worker = Task.Run(WorkerLoop);
    }

    public IReadOnlyList<TemperatureReadingDto> ReadDiskTemperatures() => _snapshot.DiskTemperatures;

    public IReadOnlyList<StorageHealthReadingDto> ReadStorageHealth() => _snapshot.StorageHealth;

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { /* ignore */ }
        try { _worker.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        _cts.Dispose();
    }

    private async Task WorkerLoop()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _snapshot = Read();
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "StorageSmartService poll failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private Snapshot Read()
    {
        var temps = new List<TemperatureReadingDto>();
        var health = new List<StorageHealthReadingDto>();

        try
        {
            using var session = CimSession.Create(null);

            var disks = session.QueryInstances(
                @"root/Microsoft/Windows/Storage",
                "WQL",
                "SELECT DeviceId, FriendlyName, Model, BusType FROM MSFT_PhysicalDisk");

            foreach (var disk in disks)
            {
                using (disk)
                {
                    ushort busType = 0;
                    var busTypeObj = disk.CimInstanceProperties["BusType"]?.Value;
                    if (busTypeObj is not null)
                    {
                        try { busType = Convert.ToUInt16(busTypeObj); } catch { /* ignore */ }
                    }

                    if (busType == 7)
                    {
                        continue;
                    }

                    var friendlyName = disk.CimInstanceProperties["FriendlyName"]?.Value as string;
                    var model = disk.CimInstanceProperties["Model"]?.Value as string;
                    var name = !string.IsNullOrWhiteSpace(friendlyName) ? friendlyName!.Trim()
                        : !string.IsNullOrWhiteSpace(model) ? model!.Trim()
                        : $"Disk {disk.CimInstanceProperties["DeviceId"]?.Value}";

                    // MSFT_StorageReliabilityCounter is not directly queryable; it is an associated
                    // instance of MSFT_PhysicalDisk. Enumerate all associated instances and pick the
                    // one we need. Filtering by ResultClass in the call returns 0 on this host, so
                    // enumerate unfiltered and filter in code (matches Get-CimAssociatedInstance behavior).
                    IEnumerable<CimInstance> assoc;
                    try
                    {
                        assoc = session.EnumerateAssociatedInstances(
                            @"root/Microsoft/Windows/Storage",
                            disk,
                            null, null, null, null);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var ro in assoc)
                    {
                        using (ro)
                        {
                            if (!string.Equals(ro.CimSystemProperties.ClassName, "MSFT_StorageReliabilityCounter", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            int? temp = null;
                            var tempObj = ro.CimInstanceProperties["Temperature"]?.Value;
                            if (tempObj is not null)
                            {
                                try { temp = Convert.ToInt32(tempObj); } catch { /* ignore */ }
                                if (temp is 0) temp = null;
                            }

                            int? wear = null;
                            var wearObj = ro.CimInstanceProperties["Wear"]?.Value;
                            if (wearObj is not null)
                            {
                                try { wear = Convert.ToInt32(wearObj); } catch { /* ignore */ }
                            }

                            if (temp.HasValue)
                            {
                                temps.Add(new TemperatureReadingDto("Disk", name, temp.Value));
                            }

                            double? life = null;
                            if (wear.HasValue)
                            {
                                life = Math.Clamp(100 - wear.Value, 0, 100);
                            }

                            if (life.HasValue)
                            {
                                health.Add(new StorageHealthReadingDto(name, life, null, null));
                            }
                            else if (temp.HasValue)
                            {
                                health.Add(new StorageHealthReadingDto(name, null, null, null));
                            }

                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to query storage reliability via CIM");
        }

        return new Snapshot(temps, health);
    }
}