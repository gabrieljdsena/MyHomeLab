using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Services;
using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;
using Xunit;

namespace MyHomeLab.Tests;

public sealed class FakeHealthHistoryRepository : IHealthHistoryRepository
{
    private readonly List<AppHealthSample> _store = [];

    public Task AddAsync(AppHealthSample sample, CancellationToken cancellationToken = default)
    {
        _store.Add(sample);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AppHealthSample>> GetRecentAsync(HealthHistoryQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = _store
            .Where(s => s.AppId == query.AppId && (query.SinceUtc == null || s.CheckedAtUtc >= query.SinceUtc))
            .OrderBy(s => s.CheckedAtUtc)
            .Take(query.Limit)
            .ToArray();
        return Task.FromResult<IReadOnlyList<AppHealthSample>>(filtered);
    }

    public IReadOnlyList<AppHealthSample> All => _store;
}

public class HealthHistoryTests
{
    [Fact]
    public void AppHealthSample_Create_Sets_Utc_And_Validates_Latency()
    {
        var sample = AppHealthSample.Create(AppId.New(), AppHealthStatus.Up, 42, DateTime.UtcNow);
        Assert.Equal(AppHealthStatus.Up, sample.Status);
        Assert.Equal(42, sample.LatencyMs);

        var ex = Assert.Throws<DomainException>(() =>
            new AppHealthSample(1, AppId.New(), AppHealthStatus.Up, -1, DateTime.UtcNow));
        Assert.Contains("Latency", ex.Message);
    }

    [Fact]
    public async Task ProbeAsync_Records_History()
    {
        var repo = new FakeAppRepository();
        var history = new FakeHealthHistoryRepository();
        var service = new AppService(repo, history);
        var app = await service.CreateAsync(new MyHomeLab.Application.Dtos.CreateAppRequest(
            "Jellyfin", "http://localhost:8096", "Media", "movie", "media", 8096, ["media"], null, true, 30_000, 0));

        var result = await service.ProbeAsync(AppId.From(app.Id), new StubUpChecker());

        Assert.Equal("up", result.Status);
        Assert.Single(history.All);
        Assert.Equal(app.Id, history.All[0].AppId.Value);
        Assert.Equal(AppHealthStatus.Up, history.All[0].Status);
    }

    [Fact]
    public async Task GetHealthHistoryAsync_Returns_Uptime()
    {
        var repo = new FakeAppRepository();
        var history = new FakeHealthHistoryRepository();
        var service = new AppService(repo, history);
        var app = await service.CreateAsync(new MyHomeLab.Application.Dtos.CreateAppRequest(
            "App", "http://localhost", "desc", "web", "other", null, [], null, true, 30_000, 0));
        var id = AppId.From(app.Id);

        await history.AddAsync(new AppHealthSample(1, id, AppHealthStatus.Up, 10, DateTime.UtcNow.AddMinutes(-2)));
        await history.AddAsync(new AppHealthSample(2, id, AppHealthStatus.Up, 20, DateTime.UtcNow.AddMinutes(-1)));
        await history.AddAsync(new AppHealthSample(3, id, AppHealthStatus.Down, null, DateTime.UtcNow));

        var dto = await service.GetHealthHistoryAsync(id, hours: 24, limit: 200);

        Assert.Equal(3, dto.Points.Count);
        Assert.Equal(3, dto.Uptime.TotalChecks);
        Assert.Equal(2, dto.Uptime.UpCount);
        Assert.Equal(1, dto.Uptime.DownCount);
        Assert.Equal(66.7, dto.Uptime.UptimePercent, 1);
        Assert.Equal(15, dto.Uptime.AverageLatencyMs);
    }

    [Fact]
    public async Task GetHealthHistoryAsync_Empty_When_NoHistory()
    {
        var repo = new FakeAppRepository();
        var history = new FakeHealthHistoryRepository();
        var service = new AppService(repo, history);
        var app = await service.CreateAsync(new MyHomeLab.Application.Dtos.CreateAppRequest(
            "App2", "http://localhost:8097", "desc", "web", "other", null, [], null, true, 30_000, 1));

        var dto = await service.GetHealthHistoryAsync(AppId.From(app.Id), 24, 200);
        Assert.Empty(dto.Points);
        Assert.Equal(0, dto.Uptime.TotalChecks);
    }

    [Fact]
    public async Task ProbeAsync_BestEffort_When_History_Fails()
    {
        var repo = new FakeAppRepository();
        var failingHistory = new FailingHistoryRepository();
        var service = new AppService(repo, failingHistory);
        var app = await service.CreateAsync(new MyHomeLab.Application.Dtos.CreateAppRequest(
            "App3", "http://localhost:8098", "desc", "web", "other", null, [], null, true, 30_000, 2));

        var result = await service.ProbeAsync(AppId.From(app.Id), new StubUpChecker());
        Assert.Equal("up", result.Status);

        var stored = await repo.GetByIdAsync(AppId.From(app.Id));
        Assert.Equal(AppHealthStatus.Up, stored!.HealthStatus);
    }

    private sealed class StubUpChecker : IHealthChecker
    {
        public Task<HealthCheckOutcome> ProbeAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HealthCheckOutcome(AppHealthStatus.Up, 7));
    }

    private sealed class FailingHistoryRepository : IHealthHistoryRepository
    {
        public Task AddAsync(AppHealthSample sample, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("db down");

        public Task<IReadOnlyList<AppHealthSample>> GetRecentAsync(HealthHistoryQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AppHealthSample>>([]);
    }
}
