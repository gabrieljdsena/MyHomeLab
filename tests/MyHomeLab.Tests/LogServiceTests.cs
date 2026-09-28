using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Errors;
using MyHomeLab.Application.Services;
using Xunit;

namespace MyHomeLab.Tests;

public class LogServiceTests
{
    private static (LogService Service, FakeLogRepository Repository) Build()
    {
        var repository = new FakeLogRepository();
        return (new LogService(repository), repository);
    }

    [Fact]
    public async Task GetAllAsync_Returns_Newest_First()
    {
        var (service, repository) = Build();
        repository.Seed("api", "first");
        repository.Seed("worker", "second");

        var all = await service.GetAllAsync(new LogQuery());

        Assert.Equal([2, 1], all.Select(e => e.Id).ToArray());
    }

    [Fact]
    public async Task GetAllAsync_Filters_By_Application_And_Search()
    {
        var (service, repository) = Build();
        repository.Seed("api", "startup ok");
        repository.Seed("worker", "startup ok");
        repository.Seed("api", "disk full");

        var byApplication = await service.GetAllAsync(new LogQuery(Application: "api"));
        Assert.Equal(2, byApplication.Count);
        Assert.All(byApplication, e => Assert.Equal("api", e.Application));

        var bySearch = await service.GetAllAsync(new LogQuery(Search: "disk"));
        Assert.Single(bySearch);
        Assert.Equal("disk full", bySearch[0].Log);
    }

    [Fact]
    public async Task GetAllAsync_Clamps_Out_Of_Range_Limit()
    {
        var (service, repository) = Build();
        repository.Seed("api", "one");

        var clamped = new LogQuery(Limit: 0);
        var all = await service.GetAllAsync(clamped);

        Assert.Single(all);
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Entry()
    {
        var (service, repository) = Build();
        repository.Seed("api", "boom");

        var entry = await service.GetByIdAsync(1);

        Assert.Equal("api", entry.Application);
        Assert.Equal("boom", entry.Log);
    }

    [Fact]
    public async Task GetByIdAsync_Missing_Throws_NotFound()
    {
        var (service, _) = Build();

        await Assert.ThrowsAsync<LogNotFoundException>(() => service.GetByIdAsync(99));
    }
}
