using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Infrastructure.Toolchain;

public sealed class ToolchainService(IOptions<ToolchainOptions> options, ILogger<ToolchainService> logger) : IToolchainService
{
    private static readonly IReadOnlyList<ToolDefinition> Tools =
    [
        new("node", ["node.exe", "node"], ["--version"], o => o.NodeExecutable),
        new("python", ["python.exe", "python3.exe", "python"], ["--version"], o => o.PythonExecutable),
        new("git", ["git.exe", "git"], ["--version"], o => o.GitExecutable),
        new("7zip", ["7z.exe", "7z"], [], o => o.SevenZipExecutable),
        new("ffmpeg", ["ffmpeg.exe", "ffmpeg"], ["-version"], o => o.FfmpegExecutable),
        new("opencode", ["opencode.exe", "opencode"], ["--version"], o => o.OpencodeExecutable),
        new("curl", ["curl.exe", "curl"], ["--version"], o => null),
    ];

    private readonly ToolchainOptions config = options.Value;

    public string? ResolvePath(string tool)
    {
        var definition = FindDefinition(tool);
        if (definition is null)
        {
            return null;
        }

        var configured = definition.Override(config)?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return ResolveInPath(
            definition,
            Environment.GetEnvironmentVariable("Path") ?? string.Empty);
    }

    internal static string? ResolveInPath(ToolDefinition definition, string pathEnv)
    {
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string fullDir;
            try
            {
                fullDir = Path.GetFullPath(dir);
            }
            catch
            {
                continue;
            }

            if (fullDir.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var candidate in definition.Executables)
            {
                if (File.Exists(Path.Combine(fullDir, candidate)))
                {
                    return Path.Combine(fullDir, candidate);
                }

                if (OperatingSystem.IsWindows() && Path.GetExtension(candidate) == string.Empty)
                {
                    var withExe = Path.Combine(fullDir, candidate + ".exe");
                    if (File.Exists(withExe))
                    {
                        return withExe;
                    }
                }
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<ToolInfo>> ProbeAllAsync(CancellationToken cancellationToken)
    {
        var results = new List<ToolInfo>(Tools.Count);
        foreach (var tool in Tools)
        {
            results.Add(await ProbeOneAsync(tool, cancellationToken));
        }

        return results;
    }

    private async Task<ToolInfo> ProbeOneAsync(ToolDefinition tool, CancellationToken cancellationToken)
    {
        var path = ResolvePath(tool.Name);
        if (string.IsNullOrWhiteSpace(path))
        {
            return new ToolInfo(tool.Name, false, null, null);
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

            var psi = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var arg in tool.VersionArgs)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);
            if (process is null)
            {
                return new ToolInfo(tool.Name, false, path, null);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            var output = ((await outputTask) + "\n" + await errorTask)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (output?.Length > 120)
            {
                output = output[..120];
            }

            return new ToolInfo(tool.Name, true, path, string.IsNullOrWhiteSpace(output) ? null : output);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Toolchain probe failed for {Tool} at {Path}", tool.Name, path);
            return new ToolInfo(tool.Name, false, path, null);
        }
        catch (OperationCanceledException)
        {
            return new ToolInfo(tool.Name, false, path, null);
        }
    }

    internal sealed record ToolDefinition(
        string Name,
        string[] Executables,
        string[] VersionArgs,
        Func<ToolchainOptions, string?> Override);

    internal static ToolDefinition? FindDefinition(string tool) =>
        Tools.FirstOrDefault(t => t.Name.Equals(tool, StringComparison.OrdinalIgnoreCase));
}
