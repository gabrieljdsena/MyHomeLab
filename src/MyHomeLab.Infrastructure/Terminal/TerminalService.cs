using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Domain;

namespace MyHomeLab.Infrastructure.Terminal;

public sealed class TerminalService(IOptions<TerminalOptions> options, ILogger<TerminalService> logger) : ITerminalService
{
    private readonly TerminalOptions config = options.Value;

    public TerminalConfigDto GetConfig()
    {
        var defaultDir = ResolveDefaultWorkingDirectory();
        var allowed = config.AllowedShells
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (allowed.Length == 0)
        {
            allowed = ["powershell", "pwsh", "cmd"];
        }

        return new TerminalConfigDto(
            config.Enabled,
            config.DefaultShell,
            allowed,
            config.TimeoutSeconds,
            config.MaxOutputBytes,
            defaultDir);
    }

    public async Task<TerminalExecuteResponse> ExecuteAsync(TerminalExecuteRequest request, CancellationToken cancellationToken)
    {
        if (!config.Enabled)
        {
            throw new DomainException("Terminal execution is disabled on this host.");
        }

        var command = request.Command?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new DomainException("Command is required.");
        }

        if (command.Length > 8192)
        {
            throw new DomainException("Command exceeds maximum length (8192).");
        }

        var shell = (request.Shell ?? config.DefaultShell).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(shell))
        {
            shell = config.DefaultShell.ToLowerInvariant();
        }

        var allowed = config.AllowedShells.Select(s => s.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!allowed.Contains(shell))
        {
            throw new DomainException($"Shell '{shell}' is not allowed. Allowed: {string.Join(", ", config.AllowedShells)}.");
        }

        var cwd = ResolveWorkingDirectory(request.Cwd);
        var executedAt = DateTime.UtcNow;

        if (IsCdCommand(command, out var cdArg))
        {
            return HandleCd(command, cdArg, shell, cwd, executedAt);
        }

        var stopwatch = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(config.TimeoutSeconds, 1, 300));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var psi = BuildProcessStartInfo(shell, command, cwd);
        var outputBuilder = new StringBuilder();
        var timedOut = false;
        int exitCode;

        try
        {
            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            var stdoutDone = new TaskCompletionSource<bool>();
            var stderrDone = new TaskCompletionSource<bool>();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    stdoutDone.TrySetResult(true);
                }
                else
                {
                    lock (outputBuilder)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    stderrDone.TrySetResult(true);
                }
                else
                {
                    lock (outputBuilder)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                }
            };

            if (!process.Start())
            {
                throw new DomainException("Failed to start terminal process.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to kill timed-out process for command {Command}", command);
                }

                try
                {
                    await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch
                {
                    // ignore
                }
            }

            if (!timedOut)
            {
                await Task.WhenAll(stdoutDone.Task, stderrDone.Task).WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            }

            exitCode = process.HasExited ? process.ExitCode : 124;
            if (timedOut && !process.HasExited)
            {
                exitCode = 124;
            }
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Terminal execution failed for {Shell} cwd {Cwd} command {Command}", shell, cwd, command);
            throw new DomainException($"Terminal execution failed: {ex.Message}");
        }

        stopwatch.Stop();

        var output = outputBuilder.ToString();
        if (timedOut)
        {
            output += $"\n[Timed out after {timeout.TotalSeconds:F0}s]";
        }

        output = TruncateOutput(output, config.MaxOutputBytes);

        logger.LogInformation(
            "Terminal exec shell={Shell} cwd={Cwd} exit={ExitCode} timedOut={TimedOut} durationMs={Duration} command={Command}",
            shell, cwd, exitCode, timedOut, stopwatch.ElapsedMilliseconds, command);

        return new TerminalExecuteResponse(
            command,
            shell,
            cwd,
            cwd,
            output,
            exitCode,
            stopwatch.ElapsedMilliseconds,
            timedOut,
            executedAt);
    }

    private static bool IsCdCommand(string command, out string? argument)
    {
        argument = null;
        var trimmed = command.Trim();
        if (trimmed.Equals("cd", StringComparison.OrdinalIgnoreCase))
        {
            argument = string.Empty;
            return true;
        }

        if (trimmed.StartsWith("cd ", StringComparison.OrdinalIgnoreCase))
        {
            argument = trimmed[3..].Trim().Trim('"', '\'');
            return true;
        }

        return false;
    }

    private TerminalExecuteResponse HandleCd(string command, string? cdArg, string shell, string cwd, DateTime executedAt)
    {
        string target;
        string output = string.Empty;
        int exitCode = 0;

        if (string.IsNullOrWhiteSpace(cdArg))
        {
            target = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(target) || !Directory.Exists(target))
            {
                target = cwd;
            }
        }
        else if (Path.IsPathRooted(cdArg))
        {
            target = cdArg;
        }
        else
        {
            target = Path.Combine(cwd, cdArg);
        }

        string resolved;
        try
        {
            resolved = Path.GetFullPath(target);
        }
        catch (Exception ex)
        {
            output = $"cd: {ex.Message}";
            exitCode = 1;
            resolved = cwd;
            return new TerminalExecuteResponse(command, shell, cwd, resolved, output, exitCode, 0, false, executedAt);
        }

        if (!Directory.Exists(resolved))
        {
            output = $"cd: no such directory: {resolved}";
            exitCode = 1;
            resolved = cwd;
        }

        return new TerminalExecuteResponse(command, shell, cwd, resolved, output, exitCode, 0, false, executedAt);
    }

    private static ProcessStartInfo BuildProcessStartInfo(string shell, string command, string cwd)
    {
        var psi = new ProcessStartInfo
        {
            WorkingDirectory = cwd,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        switch (shell)
        {
            case "powershell":
                psi.FileName = "powershell.exe";
                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-NonInteractive");
                psi.ArgumentList.Add("-Command");
                psi.ArgumentList.Add(command);
                break;
            case "pwsh":
                psi.FileName = "pwsh.exe";
                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-NonInteractive");
                psi.ArgumentList.Add("-Command");
                psi.ArgumentList.Add(command);
                break;
            case "cmd":
                psi.FileName = "cmd.exe";
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(command);
                break;
            default:
                psi.FileName = "powershell.exe";
                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-NonInteractive");
                psi.ArgumentList.Add("-Command");
                psi.ArgumentList.Add(command);
                break;
        }

        return psi;
    }

    private string ResolveWorkingDirectory(string? requestedCwd)
    {
        var candidate = requestedCwd?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return ResolveDefaultWorkingDirectory();
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(candidate);
        }
        catch (Exception ex)
        {
            throw new DomainException($"Invalid working directory '{candidate}': {ex.Message}");
        }

        if (!Directory.Exists(fullPath))
        {
            throw new DomainException($"Working directory does not exist: {fullPath}");
        }

        return fullPath;
    }

    private string ResolveDefaultWorkingDirectory()
    {
        var configured = config.DefaultWorkingDirectory?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            try
            {
                var full = Path.GetFullPath(configured);
                if (Directory.Exists(full))
                {
                    return full;
                }
            }
            catch
            {
                // fall back
            }
        }

        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDir) && Directory.Exists(baseDir))
        {
            return Path.GetFullPath(baseDir);
        }

        return Directory.GetCurrentDirectory();
    }

    private static string TruncateOutput(string output, int maxBytes)
    {
        if (string.IsNullOrEmpty(output))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetByteCount(output);
        if (bytes <= maxBytes)
        {
            return output;
        }

        var truncatedNotice = $"\n[Output truncated: {bytes} bytes > {maxBytes} bytes limit]";
        var allowedBytes = maxBytes - Encoding.UTF8.GetByteCount(truncatedNotice) - 64;
        if (allowedBytes <= 0)
        {
            return truncatedNotice;
        }

        var chars = output.ToCharArray();
        var kept = new StringBuilder();
        var currentBytes = 0;
        foreach (var ch in chars)
        {
            var charBytes = Encoding.UTF8.GetByteCount(ch.ToString());
            if (currentBytes + charBytes > allowedBytes)
            {
                break;
            }

            kept.Append(ch);
            currentBytes += charBytes;
        }

        kept.Append(truncatedNotice);
        return kept.ToString();
    }
}
