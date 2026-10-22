using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Domain;
using MyHomeLab.Infrastructure.Terminal;

namespace MyHomeLab.Tests;

public class TerminalPtyServiceTests
{
    private sealed class FakeToolchainService(string? nodePath) : IToolchainService
    {
        public string? ResolvePath(string tool) =>
            tool.Equals("node", StringComparison.OrdinalIgnoreCase) ? nodePath : null;

        public Task<IReadOnlyList<ToolInfo>> ProbeAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ToolInfo>>([]);
    }

    private static TerminalPtyService CreateService(TerminalOptions? options = null) =>
        new(Options.Create(options ?? new TerminalOptions()),
            new FakeToolchainService(null),
            NullLogger<TerminalPtyService>.Instance);

    [Fact]
    public void GetPtyConfig_Returns_Defaults()
    {
        var config = CreateService().GetPtyConfig();

        Assert.Equal("powershell", config.DefaultShell);
        Assert.Contains("powershell", config.AllowedShells);
        Assert.Equal(4, config.MaxSessions);
        Assert.Equal(1500, config.IdleTimeoutSeconds);
        Assert.Equal(14400, config.MaxLifetimeSeconds);
        Assert.NotEmpty(config.DefaultWorkingDirectory);
    }

    [Fact]
    public void GetPtyConfig_Maps_Idle_Timeout_And_Clamps()
    {
        var configured = CreateService(new TerminalOptions { PtyIdleTimeoutSeconds = 1500 }).GetPtyConfig();
        Assert.Equal(1500, configured.IdleTimeoutSeconds);

        var belowFloor = CreateService(new TerminalOptions { PtyIdleTimeoutSeconds = 10 }).GetPtyConfig();
        Assert.Equal(60, belowFloor.IdleTimeoutSeconds);

        var aboveCeiling = CreateService(new TerminalOptions { PtyIdleTimeoutSeconds = 99_999 }).GetPtyConfig();
        Assert.Equal(7200, aboveCeiling.IdleTimeoutSeconds);
    }

    [Fact]
    public void GetPtyConfig_Maps_Lifetime_And_Clamps()
    {
        var configured = CreateService(new TerminalOptions { PtyMaxLifetimeSeconds = 7200 }).GetPtyConfig();
        Assert.Equal(7200, configured.MaxLifetimeSeconds);

        var belowFloor = CreateService(new TerminalOptions { PtyMaxLifetimeSeconds = 10 }).GetPtyConfig();
        Assert.Equal(300, belowFloor.MaxLifetimeSeconds);

        var aboveCeiling = CreateService(new TerminalOptions { PtyMaxLifetimeSeconds = 999_999 }).GetPtyConfig();
        Assert.Equal(14400, aboveCeiling.MaxLifetimeSeconds);
    }

    [Fact]
    public void GetPtyConfig_Disabled_When_Pty_Off()
    {
        var config = CreateService(new TerminalOptions { PtyEnabled = false }).GetPtyConfig();

        Assert.False(config.Enabled);
    }

    [Fact]
    public async Task CreateSessionAsync_Disallowed_Shell_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateSessionAsync("bash", null, 80, 24, CancellationToken.None));
    }

    [Fact]
    public async Task CreateSessionAsync_Disabled_Throws()
    {
        var service = CreateService(new TerminalOptions { PtyEnabled = false });

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateSessionAsync("powershell", null, 80, 24, CancellationToken.None));
    }

    [Fact]
    public async Task CreateSessionAsync_Missing_Cwd_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateSessionAsync("cmd", "Z:\\definitely-not-here-myhomelab", 80, 24, CancellationToken.None));
    }

    [Fact]
    public async Task CreateSessionAsync_Second_Session_Over_Cap_Throws()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = CreateService(new TerminalOptions { PtyMaxSessions = 1 });
        await using var first = await service.CreateSessionAsync("cmd", null, 80, 24, CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateSessionAsync("cmd", null, 80, 24, CancellationToken.None));
    }

    [Fact]
    public async Task KillSessionAsync_Unknown_Id_Returns_False()
    {
        var service = CreateService();

        Assert.False(await service.KillSessionAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Empty(service.ListSessions());
    }

    [Fact]
    public async Task Session_Lives_After_Attachment_Detaches_And_Can_Be_Reattached()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = CreateService();
        var session = await service.CreateSessionAsync("cmd", null, 80, 24, CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await session.WriteInputAsync("echo detach-test-token\r", cts.Token);

        var attachment = session.Attach();
        var seen = new StringBuilder();
        while (!seen.ToString().Contains("detach-test-token", StringComparison.Ordinal))
        {
            var chunk = await attachment.ReadOutputAsync(cts.Token);
            Assert.NotNull(chunk);
            seen.Append(chunk);
        }

        await attachment.DisposeAsync();

        Assert.True(service.TryGetSession(session.Id, out _));

        var summary = Assert.Single(service.ListSessions());
        Assert.Equal(session.Id, summary.Id);
        Assert.Equal(0, summary.AttachedClients);
        Assert.True(summary.Running);

        var replay = session.Attach();
        var replayed = new StringBuilder();
        while (!replayed.ToString().Contains("detach-test-token", StringComparison.Ordinal))
        {
            var chunk = await replay.ReadOutputAsync(cts.Token);
            Assert.NotNull(chunk);
            replayed.Append(chunk);
        }

        await replay.DisposeAsync();
        Assert.True(await service.KillSessionAsync(session.Id, CancellationToken.None));
        Assert.Empty(service.ListSessions());
    }

    [Fact]
    public async Task Attach_Fans_Out_Output_To_Multiple_Clients()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = CreateService();
        await using var session = await service.CreateSessionAsync("cmd", null, 80, 24, CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var first = session.Attach();
        var second = session.Attach();
        Assert.Equal(2, session.AttachedClients);

        await session.WriteInputAsync("echo fanout-token\r", cts.Token);

        foreach (var attachment in new[] { first, second })
        {
            var seen = new StringBuilder();
            while (!seen.ToString().Contains("fanout-token", StringComparison.Ordinal))
            {
                var chunk = await attachment.ReadOutputAsync(cts.Token);
                Assert.NotNull(chunk);
                seen.Append(chunk);
            }

            await attachment.DisposeAsync();
        }

        Assert.Equal(0, session.AttachedClients);
    }
}
