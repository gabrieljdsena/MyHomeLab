using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;
using Xunit;

namespace MyHomeLab.Tests;

public class AppTests
{
    private static App CreateApp() =>
        App.Create(
            "Jellyfin",
            "http://localhost:8096",
            "Media",
            "movie",
            "media",
            8096,
            ["media", "video"],
            dockerContainer: null,
            healthCheckEnabled: true,
            healthCheckIntervalMs: 30_000,
            sortOrder: 10);

    [Fact]
    public void Create_Assigns_Identity_And_Defaults()
    {
        var app = CreateApp();

        Assert.NotEqual(Guid.Empty, app.Id.Value);
        Assert.Equal("Jellyfin", app.Name);
        Assert.Equal(AppHealthStatus.Unknown, app.HealthStatus);
        Assert.True(app.IsEnabled);
    }

    [Fact]
    public void Create_Rejects_Empty_Name()
    {
        var ex = Assert.Throws<DomainException>(() =>
            App.Create("", "http://localhost:8096", "", "web", "media", null, null, null, true, 30_000, 0));

        Assert.Equal("Name is required.", ex.Message);
    }

    [Fact]
    public void Create_Rejects_Relative_Url()
    {
        var ex = Assert.Throws<DomainException>(() =>
            App.Create("X", "/not-absolute", "", "web", "other", null, null, null, true, 30_000, 0));

        Assert.Equal("URL must be an absolute http(s) address.", ex.Message);
    }

    [Fact]
    public void Create_Rejects_NonHttpScheme()
    {
        var ex = Assert.Throws<DomainException>(() =>
            App.Create("X", "ftp://localhost", "", "web", "other", null, null, null, true, 30_000, 0));

        Assert.Equal("URL must be an absolute http(s) address.", ex.Message);
    }

    [Fact]
    public void Create_Rejects_OutOfRange_Port()
    {
        var ex = Assert.Throws<DomainException>(() =>
            App.Create("X", "http://localhost", "", "web", "other", 70000, null, null, true, 30_000, 0));

        Assert.Equal("Port must be between 1 and 65535.", ex.Message);
    }

    [Fact]
    public void Create_Rejects_OutOfRange_HealthInterval()
    {
        var ex = Assert.Throws<DomainException>(() =>
            App.Create("X", "http://localhost", "", "web", "other", null, null, null, true, 500, 0));

        Assert.Equal("Health check interval must be between 1000 and 3600000 ms.", ex.Message);
    }

    [Fact]
    public void Create_Rejects_Negative_SortOrder()
    {
        var ex = Assert.Throws<DomainException>(() =>
            App.Create("X", "http://localhost", "", "web", "other", null, null, null, true, 30_000, -1));

        Assert.Equal("Sort order cannot be negative.", ex.Message);
    }

    [Fact]
    public void Enable_And_Disable_Toggle_And_Touch()
    {
        var app = CreateApp();

        app.Disable();
        Assert.False(app.IsEnabled);

        app.Enable();
        Assert.True(app.IsEnabled);
    }

    [Fact]
    public void SetHealth_Persists_Status_And_Latency()
    {
        var app = CreateApp();
        var at = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        app.SetHealth(AppHealthStatus.Up, 42, at);

        Assert.Equal(AppHealthStatus.Up, app.HealthStatus);
        Assert.Equal(42, app.LastLatencyMs);
        Assert.Equal(at, app.LastHealthCheckUtc);
    }

    [Fact]
    public void UpdateDetails_Changes_Values_And_Bumps_UpdatedAt()
    {
        var app = CreateApp();
        var before = app.UpdatedAtUtc;

        app.UpdateDetails("New Name", "http://localhost:1234", "New desc", "memory", "ai", 1234, ["llm"], null, false, 60_000, 5);

        Assert.Equal("New Name", app.Name);
        Assert.Equal("http://localhost:1234", app.Url);
        Assert.Equal("ai", app.Category);
        Assert.False(app.HealthCheckEnabled);
        Assert.True(app.UpdatedAtUtc >= before);
    }

    [Fact]
    public void MoveTo_Updates_SortOrder()
    {
        var app = CreateApp();

        app.MoveTo(99);

        Assert.Equal(99, app.SortOrder);
    }

    [Fact]
    public void Restore_Builds_Entity_From_Persistence_State()
    {
        var app = App.Restore(
            AppId.New(),
            "Jellyfin",
            "Media",
            "http://localhost:8096",
            "movie",
            "media",
            8096,
            ["media"],
            null,
            true,
            30_000,
            AppHealthStatus.Up,
            DateTime.UtcNow,
            12,
            true,
            3,
            DateTime.UtcNow,
            DateTime.UtcNow);

        Assert.Equal(AppHealthStatus.Up, app.HealthStatus);
        Assert.Equal(12, app.LastLatencyMs);
        Assert.Equal(3, app.SortOrder);
    }

    [Fact]
    public void Create_Normalizes_DockerContainer()
    {
        var app = App.Create("X", "http://localhost", "desc", "web", "other", null, null, "  pihole  ", true, 30_000, 0);
        Assert.Equal("pihole", app.DockerContainer);

        var empty = App.Create("Y", "http://localhost:8081", "desc", "web", "other", null, null, "   ", true, 30_000, 0);
        Assert.Null(empty.DockerContainer);

        var nullContainer = App.Create("Z", "http://localhost:8082", "desc", "web", "other", null, null, null, true, 30_000, 0);
        Assert.Null(nullContainer.DockerContainer);
    }

    [Fact]
    public void Create_Rejects_Invalid_DockerContainer()
    {
        var ex = Assert.Throws<DomainException>(() =>
            App.Create("X", "http://localhost", "desc", "web", "other", null, null, "bad name!", true, 30_000, 0));
        Assert.Contains("Docker container", ex.Message);
    }

    [Fact]
    public void UpdateDetails_Sets_And_Clears_DockerContainer()
    {
        var app = CreateApp();
        app.UpdateDetails("X", "http://localhost:1234", "desc", "web", "other", null, null, "my-container_1", true, 30_000, 0);
        Assert.Equal("my-container_1", app.DockerContainer);

        app.UpdateDetails("X", "http://localhost:1234", "desc", "web", "other", null, null, "  ", true, 30_000, 0);
        Assert.Null(app.DockerContainer);
    }
}
