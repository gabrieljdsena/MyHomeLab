using Microsoft.Extensions.Logging.Abstractions;
using MyHomeLab.Api.Services;

namespace MyHomeLab.Tests;

public class SystemMetricsServiceTests
{
    [Theory]
    [InlineData(75, 100, 25)]
    [InlineData(60, 100, 40)]
    [InlineData(0, 100, 0)]
    [InlineData(110, 100, 0)]
    public void CpuUsagePercent_Computes_IdleVsTotal(long idleDelta, long totalDelta, double expected)
    {
        Assert.Equal(expected, SystemMetricsService.CpuUsagePercent(idleDelta, totalDelta));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 0)]
    [InlineData(-5, 100)]
    [InlineData(-10, 10)]
    public void CpuUsagePercent_Invalid_Deltas_Return_Zero(long idleDelta, long totalDelta)
    {
        Assert.Equal(0, SystemMetricsService.CpuUsagePercent(idleDelta, totalDelta));
    }

    [Fact]
    public void GetMetrics_OnWindows_Reports_Memory_And_Disks()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var metrics = new SystemMetricsService().GetMetrics();

        Assert.True(metrics.TotalMemoryBytes > 0);
        Assert.True(metrics.AvailableMemoryBytes > 0);
        Assert.True(metrics.ProcessorCount > 0);
    }

    [Fact]
    public void ReadTemperatures_Does_Not_Throw()
    {
        using var service = new SystemSensorService(NullLogger<SystemSensorService>.Instance);

        var readings = service.ReadTemperatures();

        Assert.NotNull(readings);
    }

    [Fact]
    public void ReadStorageHealth_Does_Not_Throw()
    {
        using var service = new SystemSensorService(NullLogger<SystemSensorService>.Instance);

        var readings = service.ReadStorageHealth();

        Assert.NotNull(readings);
    }
}