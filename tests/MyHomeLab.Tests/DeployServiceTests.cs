using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyHomeLab.Domain;
using MyHomeLab.Infrastructure.Deploy;

namespace MyHomeLab.Tests;

public class DeployServiceTests : IDisposable
{
    private readonly List<string> _tempDirs = [];
    private string _stateRoot = string.Empty;

    private DeployService CreateService(int outputTailChars = 4000)
    {
        _stateRoot = Directory.CreateTempSubdirectory("myhomelab-deploys-").FullName;
        _tempDirs.Add(_stateRoot);
        return new DeployService(
            Options.Create(new DeployOptions
            {
                TimeoutSeconds = 1800,
                OutputTailChars = outputTailChars,
                StateDirectory = _stateRoot,
            }),
            NullLogger<DeployService>.Instance);
    }

    private string AppDirectory(Guid appId) => Path.Combine(_stateRoot, appId.ToString("N"));

    private async Task WriteStatusFixtureAsync(Guid appId, string state, string message, DateTime updatedAtUtc, int? exitCode = null)
    {
        var dir = AppDirectory(appId);
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(new
        {
            state,
            message,
            exitCode,
            startedAtUtc = updatedAtUtc,
            updatedAtUtc,
        });
        await File.WriteAllTextAsync(Path.Combine(dir, "status.json"), json);
    }

    private async Task<string> CreateScriptAsync(string content = "Write-Output 'built'")
    {
        var dir = Directory.CreateTempSubdirectory("myhomelab-script-");
        _tempDirs.Add(dir.FullName);
        var path = Path.Combine(dir.FullName, "build.ps1");
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    [Fact]
    public async Task GetDeployStatusAsync_Returns_Null_When_No_Deploy_Exists()
    {
        var service = CreateService();

        var status = await service.GetDeployStatusAsync(Guid.NewGuid());

        Assert.Null(status);
    }

    [Fact]
    public async Task GetDeployStatusAsync_Reads_Status_File_And_Tails_Output()
    {
        var service = CreateService(outputTailChars: 100);
        var appId = Guid.NewGuid();
        var updatedAt = DateTime.UtcNow;
        await WriteStatusFixtureAsync(appId, "failed", "Build failed (exit code 3).", updatedAt, exitCode: 3);

        var dir = AppDirectory(appId);
        var log = new string('x', 4900) + "TAIL-MARKER-" + new string('y', 87);
        await File.WriteAllTextAsync(Path.Combine(dir, "deploy.log"), log);

        var status = await service.GetDeployStatusAsync(appId);

        Assert.NotNull(status);
        Assert.Equal(appId, status.AppId);
        Assert.Equal("failed", status.State);
        Assert.Equal("Build failed (exit code 3).", status.Message);
        Assert.Equal(3, status.ExitCode);
        Assert.Equal(log[^100..], status.OutputTail);
        Assert.True(Math.Abs((updatedAt - status.UpdatedAtUtc).TotalSeconds) < 1);
    }

    [Fact]
    public async Task StartDeployAsync_Rejects_Invalid_Service_Name()
    {
        var service = CreateService();
        var script = await CreateScriptAsync();

        await Assert.ThrowsAsync<DomainException>(
            () => service.StartDeployAsync(Guid.NewGuid(), "bad;name", script));
    }

    [Fact]
    public async Task StartDeployAsync_Missing_Script_Throws()
    {
        var service = CreateService();
        var missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.ps1");

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => service.StartDeployAsync(Guid.NewGuid(), "EventLog", missing));

        Assert.Contains("was not found", ex.Message);
    }

    [Fact]
    public async Task StartDeployAsync_Returns_Active_Deploy_Without_Starting_Another()
    {
        var service = CreateService();
        var appId = Guid.NewGuid();
        var script = await CreateScriptAsync();
        await WriteStatusFixtureAsync(appId, "building", "Running build...", DateTime.UtcNow);

        var status = await service.StartDeployAsync(appId, "EventLog", script);

        Assert.Equal("building", status.State);
        Assert.Equal("Running build...", status.Message);
        Assert.False(File.Exists(Path.Combine(AppDirectory(appId), "deploy.ps1")));
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Temp cleanup is best-effort.
            }
        }
    }
}
