using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Domain;

namespace MyHomeLab.Infrastructure.System;

public sealed class PowerService(ILogger<PowerService> logger) : IPowerService
{
    public async Task<PowerResponse> ExecuteAsync(string action, CancellationToken cancellationToken)
    {
        var normalized = action?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized is not ("shutdown" or "reboot" or "poweroff" or "restart"))
        {
            throw new DomainException("Action must be 'shutdown' or 'reboot'.");
        }

        var isShutdown = normalized is "shutdown" or "poweroff";
        var requestedAt = DateTime.UtcNow;

        var psi = BuildShutdownStartInfo(isShutdown);

        logger.LogWarning("Power action requested: {Action} via {FileName} {Args}", normalized, psi.FileName, string.Join(" ", psi.ArgumentList));

        try
        {
            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            if (!process.Start())
            {
                throw new DomainException("Failed to start power command.");
            }

            // Shutdown is fire-and-forget; wait briefly to capture immediate errors.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // timed out waiting — likely shutdown is pending, accept as success
                return new PowerResponse(normalized, true, requestedAt, "Shutdown scheduled.");
            }

            if (process.ExitCode != 0)
            {
                var detail = $"Exit code {process.ExitCode}";
                logger.LogWarning("Power command {Action} exited with {ExitCode}", normalized, process.ExitCode);
                return new PowerResponse(normalized, false, requestedAt, detail);
            }

            return new PowerResponse(normalized, true, requestedAt, isShutdown ? "Shutdown scheduled." : "Reboot scheduled.");
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Power action {Action} failed", normalized);
            throw new DomainException($"Power action failed: {ex.Message}");
        }
    }

    private static ProcessStartInfo BuildShutdownStartInfo(bool isShutdown)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = "shutdown",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ArgumentList = { isShutdown ? "/s" : "/r", "/t", "0" },
            };
        }

        // Linux / macOS fallback — requires privilege
        return new ProcessStartInfo
        {
            FileName = "shutdown",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { isShutdown ? "-h" : "-r", "now" },
        };
    }
}
