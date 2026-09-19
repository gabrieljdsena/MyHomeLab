using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Errors;
using MyHomeLab.Application.Services;
using MyHomeLab.Domain;
using Xunit;

namespace MyHomeLab.Tests;

public class AppServiceTests
{
    private static CreateAppRequest CreateRequest(string name = "Jellyfin") =>
        new(
            name,
            "http://localhost:8096",
            "Media",
            "movie",
            "media",
            8096,
            ["media", "video"],
            HealthCheckEnabled: true,
            HealthCheckIntervalMs: 30_000,
            SortOrder: 10);

    [Fact]
    public async Task CreateAsync_Adds_App_And_Returns_Detail()
    {
        var service = new AppService(new FakeAppRepository());

        var created = await service.CreateAsync(CreateRequest());

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Jellyfin", created.Name);
        Assert.Equal("unknown", created.HealthStatus);
    }

    [Fact]
    public async Task CreateAsync_Duplicate_Name_Throws_Conflict()
    {
        var repository = new FakeAppRepository();
        var service = new AppService(repository);
        await service.CreateAsync(CreateRequest());

        await Assert.ThrowsAsync<AppConflictException>(() => service.CreateAsync(CreateRequest()));
    }

    [Fact]
    public async Task GetByIdAsync_Missing_Throws_NotFound()
    {
        var service = new AppService(new FakeAppRepository());

        await Assert.ThrowsAsync<AppNotFoundException>(() => service.GetByIdAsync(AppId.New()));
    }

    [Fact]
    public async Task PatchAsync_Only_Applies_Provided_Fields()
    {
        var repository = new FakeAppRepository();
        var service = new AppService(repository);
        var created = await service.CreateAsync(CreateRequest());

        var patched = await service.PatchAsync(
            AppId.From(created.Id),
            new PatchAppRequest(
                Name: null,
                Url: null,
                Description: "Patched desc",
                Icon: null,
                Category: null,
                Port: null,
                Tags: null,
                HealthCheckEnabled: null,
                IsEnabled: false,
                HealthCheckIntervalMs: null,
                SortOrder: null));

        Assert.Equal("Jellyfin", patched.Name);
        Assert.Equal("Patched desc", patched.Description);
        Assert.False(patched.IsEnabled);
        Assert.Equal("media", patched.Category);
    }

    [Fact]
    public async Task DeleteAsync_Missing_Throws_NotFound()
    {
        var service = new AppService(new FakeAppRepository());

        await Assert.ThrowsAsync<AppNotFoundException>(() => service.DeleteAsync(AppId.New()));
    }

    [Fact]
    public async Task ProbeAsync_Updates_Health_State()
    {
        var repository = new FakeAppRepository();
        var service = new AppService(repository);
        var created = await service.CreateAsync(CreateRequest());

        var result = await service.ProbeAsync(AppId.From(created.Id), new StubHealthChecker(), CancellationToken.None);

        Assert.Equal("up", result.Status);
        Assert.Equal(7, result.LatencyMs);

        var stored = await repository.GetByIdAsync(AppId.From(created.Id));
        Assert.Equal(AppHealthStatus.Up, stored!.HealthStatus);
        Assert.Equal(7, stored.LastLatencyMs);
    }

    [Fact]
    public async Task GetCategoriesAsync_Merges_Stored_And_Known()
    {
        var repository = new FakeAppRepository();
        var service = new AppService(repository);
        await service.CreateAsync(CreateRequest());
        await service.CreateAsync(CreateRequest("Pihole") with
        {
            Category = "network",
            Url = "http://localhost",
            Icon = "dns",
        });

        var categories = await service.GetCategoriesAsync();

        Assert.Contains("media", categories);
        Assert.Contains("network", categories);
        Assert.Contains("automation", categories);
    }

    private sealed class StubHealthChecker : IHealthChecker
    {
        public Task<HealthCheckOutcome> ProbeAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HealthCheckOutcome(AppHealthStatus.Up, 7));
    }
}
