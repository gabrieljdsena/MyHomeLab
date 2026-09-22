using MyHomeLab.Api.Services;
using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Tests;

public class NetworkThroughputServiceTests
{
    [Theory]
    [InlineData(100, 0, 10, 10)]
    [InlineData(1000, 500, 5, 100)]
    [InlineData(0, 0, 5, 0)]
    [InlineData(100, 200, 5, 0)]
    [InlineData(100, 0, 0, 0)]
    public void RatePerSecond_Computes_Over_Elapsed_Window(long current, long previous, double seconds, double expected)
    {
        Assert.Equal(expected, NetworkThroughputService.RatePerSecond(current, previous, seconds));
    }

    [Fact]
    public void First_Poll_Establishes_Baseline_And_Returns_No_Samples()
    {
        var service = CreateService(minIntervalSeconds: 4);

        Assert.Empty(service.GetSamples());
    }

    [Fact]
    public void Produces_Sample_Once_MinInterval_Between_Polls_Elapses()
    {
        var time = 0.0;
        var service = CreateService(minIntervalSeconds: 4, time: () => time, counters: () => (time * 100, time * 50));

        service.GetSamples();
        time += 5;

        IReadOnlyList<NetworkSampleDto> samples = service.GetSamples();

        Assert.Single(samples);
        Assert.Equal(100, samples[0].DownloadBytesPerSec);
        Assert.Equal(50, samples[0].UploadBytesPerSec);
    }

    [Fact]
    public void Skips_Sampling_Within_MinInterval()
    {
        var time = 0.0;
        var service = CreateService(minIntervalSeconds: 4, time: () => time, counters: () => (time * 100, time * 50));

        service.GetSamples();
        time += 2;

        Assert.Empty(service.GetSamples());

        time += 4;

        var samples = service.GetSamples();

        Assert.Single(samples);
        Assert.Equal(100, samples[0].DownloadBytesPerSec);
    }

    [Fact]
    public void Trims_Oldest_Samples_At_Capacity()
    {
        var time = 0.0;
        var service = CreateService(capacity: 3, minIntervalSeconds: 1, time: () => time, counters: () => (time * 100, time * 50));

        service.GetSamples();
        IReadOnlyList<NetworkSampleDto> samples = [];
        for (var i = 0; i < 6; i++)
        {
            time += 2;
            samples = service.GetSamples();
        }

        Assert.Equal(3, samples.Count);
        Assert.Equal(8, samples[0].SampledAtUtc.Second);
        Assert.Equal(100, samples[^1].DownloadBytesPerSec);
    }

    [Fact]
    public void Rebases_Without_Sample_When_Counters_Reset()
    {
        var time = 0.0;
        var inBytes = 1000.0;
        var outBytes = 1000.0;
        var service = CreateService(minIntervalSeconds: 4, time: () => time, counters: () => (inBytes, outBytes));

        Assert.Empty(service.GetSamples());

        inBytes = 900;
        time += 5;

        Assert.Empty(service.GetSamples());

        inBytes = 1400;
        time += 5;

        var samples = service.GetSamples();

        Assert.Single(samples);
        Assert.Equal(100, samples[0].DownloadBytesPerSec);
        Assert.Equal(0, samples[0].UploadBytesPerSec);
    }

    [Fact]
    public void ReadInterfaceCounters_Does_Not_Throw()
    {
        var (inBytes, outBytes) = NetworkThroughputService.ReadInterfaceCounters();

        Assert.True(inBytes >= 0);
        Assert.True(outBytes >= 0);
    }

    private static NetworkThroughputService CreateService(
        int capacity = 16,
        double minIntervalSeconds = 4,
        Func<double>? time = null,
        Func<(double In, double Out)>? counters = null)
    {
        var current = 0.0;
        time ??= () => current;
        counters ??= () => (current * 100, current * 50);
        var origin = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime Clock() => origin.AddSeconds(time());

        return new NetworkThroughputService(
            capacity,
            TimeSpan.FromSeconds(minIntervalSeconds),
            Clock,
            () =>
            {
                var (readIn, readOut) = counters();
                return ((long)readIn, (long)readOut);
            });
    }
}