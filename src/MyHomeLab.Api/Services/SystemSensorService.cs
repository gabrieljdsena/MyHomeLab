using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;
using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Api.Services;

public sealed class SystemSensorService : IDisposable
{
    private readonly object _gate = new();
    private readonly Computer _computer;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _worker;
    private readonly ILogger<SystemSensorService> _logger;
    private volatile Snapshot _snapshot = new([], []);

    private sealed record Snapshot(
        IReadOnlyList<TemperatureReadingDto> Temperatures,
        IReadOnlyList<StorageHealthReadingDto> StorageHealth);

    public SystemSensorService(ILogger<SystemSensorService> logger)
    {
        _logger = logger;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsStorageEnabled = false,
            IsMotherboardEnabled = true,
        };

        _worker = Task.Run(WorkerLoop);
    }

    public IReadOnlyList<TemperatureReadingDto> ReadTemperatures() => _snapshot.Temperatures;

    public IReadOnlyList<StorageHealthReadingDto> ReadStorageHealth() => _snapshot.StorageHealth;

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { /* ignore */ }
        try { _worker.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        _cts.Dispose();
        try { _computer.Close(); } catch { /* ignore */ }
    }

    private async Task WorkerLoop()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // LHM 0.9.5+ dropped WinRing0 for PawnIO: without the PawnIO driver installed, every
        // CPU/motherboard sensor enumerates but stays null (temps show as unavailable).
        LogPawnIoStatus();

        // Computer.Open() with IsStorageEnabled=true hangs indefinitely on this host (DELL-SERVER1:
        // NVMe ADATA + USB Seagate bridge). We disabled storage and guard Open with a timeout so a
        // future hang on CPU/Motherboard still doesn't block temperatures forever.
        try
        {
            var openTask = Task.Run(() => _computer.Open(), _cts.Token);
            var completed = await Task.WhenAny(openTask, Task.Delay(TimeSpan.FromSeconds(5), _cts.Token));
            if (completed != openTask)
            {
                _logger.LogWarning("SystemSensorService Computer.Open() timed out");
                return;
            }

            await openTask;
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SystemSensorService Computer.Open() failed");
            return;
        }

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _snapshot = Read();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "SystemSensorService poll failed");
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

    private void LogPawnIoStatus()
    {
        try
        {
            if (!PawnIo.IsInstalled)
            {
                _logger.LogWarning(
                    "PawnIO driver not installed - CPU/motherboard temperatures will be unavailable. " +
                    "Run scripts/install-pawnio.ps1 from an elevated shell.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PawnIO status check failed");
        }
    }

    private Snapshot Read()
    {
        var temps = new List<TemperatureReadingDto>();
        var health = new List<StorageHealthReadingDto>();

        lock (_gate)
        {
            foreach (var hardware in Flatten(_computer.Hardware))
            {
                hardware.Update();

                var hottest = hardware.Sensors
                    .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
                    .OrderByDescending(s => s.Value)
                    .FirstOrDefault();

                if (hottest is not null)
                {
                    temps.Add(new TemperatureReadingDto(
                        ComponentName(hardware.HardwareType),
                        hottest.Name,
                        hottest.Value.GetValueOrDefault()));
                }

                if (hardware.HardwareType == HardwareType.Storage)
                {
                    var byName = hardware.Sensors
                        .Where(s => s.Value.HasValue)
                        .ToDictionary(s => s.Name, s => (double?)s.Value.GetValueOrDefault(), StringComparer.OrdinalIgnoreCase);

                    var life = byName.GetValueOrDefault("Life");
                    var written = byName.GetValueOrDefault("Data Written");
                    var hours = byName.GetValueOrDefault("Power On Hours");

                    if (life.HasValue || written is > 0 || hours is > 0)
                    {
                        health.Add(new StorageHealthReadingDto(
                            hardware.Name,
                            life,
                            written is > 0 ? (long)written.Value : null,
                            hours is > 0 ? (long)hours.Value : null));
                    }
                }
            }
        }

        return new Snapshot(temps, health);
    }

    private static IEnumerable<IHardware> Flatten(IEnumerable<IHardware> hardware)
    {
        foreach (var item in hardware)
        {
            yield return item;
            foreach (var sub in item.SubHardware)
            {
                yield return sub;
            }
        }
    }

    private static string ComponentName(HardwareType type) => type switch
    {
        HardwareType.Cpu => "CPU",
        HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => "GPU",
        HardwareType.Storage => "Disk",
        HardwareType.Motherboard => "Board",
        _ => type.ToString(),
    };
}