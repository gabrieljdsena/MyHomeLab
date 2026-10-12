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
        Assert.NotEmpty(config.DefaultWorkingDirectory);
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
    public async Task Pty_Session_Echoes_Command_Output()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = CreateService();
        await using var session = await service.CreateSessionAsync("cmd", null, 80, 24, CancellationToken.None);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await session.WriteInputAsync("echo hello-pty-test\r", cts.Token);

        var seen = new StringBuilder();
        while (!seen.ToString().Contains("hello-pty-test", StringComparison.Ordinal))
        {
            var chunk = await session.ReadOutputAsync(cts.Token);
            Assert.NotNull(chunk);
            seen.Append(chunk);
        }
    }
}
