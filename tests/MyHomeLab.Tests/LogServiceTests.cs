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
    public async Task GetPagedAsync_Returns_Newest_First_With_Total()
    {
        var (service, repository) = Build();
        repository.Seed("api", "first");
        repository.Seed("worker", "second");

        var page = await service.GetPagedAsync(new LogQuery());

        Assert.Equal([2, 1], page.Items.Select(e => e.Id).ToArray());
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_Filters_By_Application_And_Search()
    {
        var (service, repository) = Build();
        repository.Seed("api", "startup ok");
        repository.Seed("worker", "startup ok");
        repository.Seed("api", "disk full");

        var byApplication = await service.GetPagedAsync(new LogQuery(Application: "api"));
        Assert.Equal(2, byApplication.TotalCount);
        Assert.All(byApplication.Items, e => Assert.Equal("api", e.Application));

        var bySearch = await service.GetPagedAsync(new LogQuery(Search: "disk"));
        Assert.Single(bySearch.Items);
        Assert.Equal("disk full", bySearch.Items[0].Log);
    }

    [Fact]
    public async Task GetPagedAsync_Paginates_Items()
    {
        var (service, repository) = Build();
        for (var i = 0; i < 5; i++)
        {
            repository.Seed("api", $"log {i}");
        }

        var first = await service.GetPagedAsync(new LogQuery(Page: 1, PageSize: 2));
        var second = await service.GetPagedAsync(new LogQuery(Page: 2, PageSize: 2));
        var third = await service.GetPagedAsync(new LogQuery(Page: 3, PageSize: 2));

        Assert.Equal([5, 4], first.Items.Select(e => e.Id).ToArray());
        Assert.Equal([3, 2], second.Items.Select(e => e.Id).ToArray());
        Assert.Equal([1], third.Items.Select(e => e.Id).ToArray());
        Assert.Equal(5, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
    }

    [Fact]
    public async Task GetPagedAsync_Falls_Back_To_Default_PageSize_When_Out_Of_Range()
    {
        var (service, repository) = Build();
        repository.Seed("api", "one");

        var page = await service.GetPagedAsync(new LogQuery(PageSize: 0));

        Assert.Single(page.Items);
        Assert.Equal(50, page.PageSize);
    }

    [Fact]
    public async Task GetApplicationsAsync_Returns_Distinct_Sorted()
    {
        var (service, repository) = Build();
        repository.Seed("worker", "one");
        repository.Seed("api", "two");
        repository.Seed("api", "three");

        var applications = await service.GetApplicationsAsync();

        Assert.Equal(["api", "worker"], applications.ToArray());
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
