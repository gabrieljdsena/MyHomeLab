using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyHomeLab.Infrastructure.Toolchain;

namespace MyHomeLab.Tests;

public class ToolchainServiceTests : IDisposable
{
    private readonly List<string> tempDirs = [];

    private static ToolchainService CreateService(ToolchainOptions? options = null) =>
        new(Options.Create(options ?? new ToolchainOptions()), NullLogger<ToolchainService>.Instance);

    [Fact]
    public void ResolvePath_Unknown_Tool_Returns_Null()
    {
        Assert.Null(CreateService().ResolvePath("definitely-not-a-tool"));
    }

    [Fact]
    public void ResolvePath_Configured_Override_Wins_Without_Path_Search()
    {
        var service = CreateService(new ToolchainOptions { NodeExecutable = "C:\\custom\\node.exe" });

        Assert.Equal("C:\\custom\\node.exe", service.ResolvePath("node"));
    }

    [Fact]
    public void ResolvePath_Finds_Executable_On_Path()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "node.exe"), string.Empty);
        var definition = ToolchainService.FindDefinition("node");
        Assert.NotNull(definition);

        Assert.Equal(
            Path.Combine(dir, "node.exe"),
            ToolchainService.ResolveInPath(definition, dir));
    }

    [Fact]
    public void ResolvePath_Skips_WindowsApps_Stubs()
    {
        var dir = Path.Combine(NewTempDir(), "WindowsApps");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "node.exe"), string.Empty);
        var definition = ToolchainService.FindDefinition("node");
        Assert.NotNull(definition);

        Assert.Null(ToolchainService.ResolveInPath(definition, dir));
    }

    [Fact]
    public async Task ProbeAllAsync_Reports_All_Known_Tools_Without_Throwing()
    {
        var status = await CreateService().ProbeAllAsync(CancellationToken.None);

        Assert.Equal(7, status.Count);
        Assert.Equal(
            ["node", "python", "git", "7zip", "ffmpeg", "opencode", "curl"],
            status.Select(t => t.Name));
        var node = status.First(t => t.Name == "node");
        if (node.Found)
        {
            Assert.False(string.IsNullOrWhiteSpace(node.Version));
        }
        else
        {
            Assert.Null(node.Path);
        }
    }

    [Fact]
    public async Task ProbeAllAsync_Cancelled_Marks_Everything_Not_Found()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var status = await CreateService().ProbeAllAsync(cts.Token);

        Assert.NotEmpty(status);
        Assert.All(status, t => Assert.False(t.Found));
    }

    public void Dispose()
    {
        foreach (var dir in tempDirs)
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                // best effort cleanup
            }
        }

        GC.SuppressFinalize(this);
    }

    private string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"toolchain-test-{Guid.NewGuid:N}");
        Directory.CreateDirectory(dir);
        tempDirs.Add(dir);
        return dir;
    }
}
