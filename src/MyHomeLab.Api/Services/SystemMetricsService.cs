using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Api.Services;

public sealed class SystemMetricsService
{
    private readonly object _gate = new();
    private (ulong Idle, ulong Kernel, ulong User) _previousTimes;
    private double _lastCpuPercent;

    public SystemMetricsDto GetMetrics()
    {
        double cpuPercent = 0;
        MemorySnapshot memory = default;

        if (OperatingSystem.IsWindows())
        {
            (cpuPercent, memory) = ReadWindowsCounters();
        }

        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

        return new SystemMetricsDto(
            Environment.MachineName,
            Environment.OSVersion.VersionString,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription,
            Environment.ProcessorCount,
            cpuPercent,
            unchecked((long)memory.Total),
            unchecked((long)memory.Available),
            unchecked((long)(memory.Total - memory.Available)),
            memory.Total <= 0 ? 0 : Percent(unchecked((long)(memory.Total - memory.Available)), unchecked((long)memory.Total)),
            (long)uptime.TotalSeconds,
            DateTime.UtcNow,
            ReadDisks(),
            [],
            []);
    }

    [SupportedOSPlatform("windows")]
    private (double CpuPercent, MemorySnapshot Memory) ReadWindowsCounters() => (SampleCpu(), ReadMemory());

    [SupportedOSPlatform("windows")]
    private double SampleCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return _lastCpuPercent;
        }

        lock (_gate)
        {
            var idleDelta = idle - (long)_previousTimes.Idle;
            var totalDelta = (kernel + user) - (long)(_previousTimes.Kernel + _previousTimes.User);

            if (idleDelta > 0 && totalDelta > 0)
            {
                _lastCpuPercent = CpuUsagePercent(idleDelta, totalDelta);
            }

            _previousTimes = ((ulong)idle, (ulong)kernel, (ulong)user);
        }

        return _lastCpuPercent;
    }

    [SupportedOSPlatform("windows")]
    private MemorySnapshot ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return default;
        }

        return new MemorySnapshot(status.TotalPhysical, status.AvailablePhysical);
    }

    private static IReadOnlyList<DiskMetricDto> ReadDisks()
    {
        var result = new List<DiskMetricDto>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                var used = drive.TotalSize - drive.AvailableFreeSpace;
                result.Add(new DiskMetricDto(
                    drive.Name,
                    drive.DriveType.ToString(),
                    drive.DriveFormat,
                    drive.TotalSize,
                    drive.AvailableFreeSpace,
                    used,
                    Percent(used, drive.TotalSize)));
            }
            catch
            {
                // drive disappeared or denied access; skip it
            }
        }

        return result;
    }

    internal static double CpuUsagePercent(long idleDelta, long totalDelta) =>
        idleDelta <= 0 || totalDelta <= 0
            ? 0
            : Math.Clamp(100 * (1.0 - (double)idleDelta / totalDelta), 0, 100);

    private static double Percent(long used, long total) =>
        total <= 0 ? 0 : Math.Round(used * 100.0 / total, 1);

    private readonly record struct MemorySnapshot(ulong Total, ulong Available);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);
}