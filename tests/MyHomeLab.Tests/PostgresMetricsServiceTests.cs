using MyHomeLab.Api.Services;
using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Tests;

public class PostgresMetricsServiceTests
{
    private static readonly DateTime T0 = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

    private static PostgresCounters Counters(
        long committed = 1000,
        long rolledBack = 10,
        long deadlocks = 0,
        long blocksRead = 100,
        long blocksHit = 9_900,
        long tempBytes = 0,
        long walBytes = 5_000_000,
        long checkpoints = 20,
        int seconds = 0,
        double? sizeBytes = null) =>
        new(
            "18.6", "myhomelab", 42_609, 18_568_895,
            2, 100, 1, 1, 0, 0,
            committed, rolledBack, deadlocks,
            blocksRead, blocksHit, tempBytes,
            walBytes, checkpoints,
            T0.AddSeconds(seconds));

    [Fact]
    public void First_Sample_Reports_Gauges_But_Zero_Rates()
    {
        var dto = PostgresMetricsService.Project(Counters(), previous: null);

        Assert.Equal(1010, dto.TransactionsTotal);
        Assert.Equal(0, dto.TransactionsPerSecond);
        Assert.Equal(0, dto.WalBytesPerSecond);
        Assert.Equal(0, dto.TempBytesPerSecond);
        Assert.Equal(0.99, dto.CacheHitRatio);
    }

    [Fact]
    public void Rates_Are_Derived_By_Differencing_The_Previous_Sample()
    {
        var previous = Counters(seconds: 0);
        var current = Counters(committed: 2000, walBytes: 5_200_000, tempBytes: 1_000_000, seconds: 10);

        var dto = PostgresMetricsService.Project(current, previous);

        Assert.Equal(100, dto.TransactionsPerSecond);
        Assert.Equal(20_000, dto.WalBytesPerSecond);
        Assert.Equal(100_000, dto.TempBytesPerSecond);
    }

    [Fact]
    public void Counter_Reset_Yields_Zero_Rates_Instead_Of_A_Spike()
    {
        // Postgres restarted between the two samples: every cumulative counter went backwards.
        var previous = Counters(committed: 9_000, walBytes: 900_000_000, checkpoints: 500, seconds: 0);
        var current = Counters(committed: 10, walBytes: 1_000, checkpoints: 1, seconds: 10);

        var dto = PostgresMetricsService.Project(current, previous);

        Assert.Equal(0, dto.TransactionsPerSecond);
        Assert.Equal(0, dto.WalBytesPerSecond);
        Assert.Equal(0, dto.TempBytesPerSecond);
        Assert.Equal(1, dto.CheckpointsTotal);
    }

    [Fact]
    public void Non_Positive_Window_Reports_Zero_Rates()
    {
        var previous = Counters(seconds: 10);

        var sameInstant = PostgresMetricsService.Project(Counters(committed: 5000, seconds: 10), previous);
        var backwardsClock = PostgresMetricsService.Project(Counters(committed: 5000, seconds: 5), previous);

        Assert.Equal(0, sameInstant.TransactionsPerSecond);
        Assert.Equal(0, backwardsClock.TransactionsPerSecond);
    }

    [Fact]
    public void Cache_Hit_Ratio_Is_A_Cumulative_Gauge_Not_A_Rate()
    {
        var previous = Counters(blocksRead: 100, blocksHit: 900, seconds: 0);
        var current = Counters(blocksRead: 200, blocksHit: 8_000, seconds: 10);

        var dto = PostgresMetricsService.Project(current, previous);

        Assert.Equal(0.9756, dto.CacheHitRatio);
    }

    [Fact]
    public void No_Block_Activity_Reports_Zero_Cache_Hit_Ratio_Instead_Of_NaN()
    {
        var dto = PostgresMetricsService.Project(Counters(blocksRead: 0, blocksHit: 0), previous: null);

        Assert.Equal(0, dto.CacheHitRatio);
    }
}
