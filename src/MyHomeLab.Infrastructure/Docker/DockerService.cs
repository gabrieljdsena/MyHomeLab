using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Domain;

namespace MyHomeLab.Infrastructure.Docker;

public sealed class DockerService(
    IOptions<DockerOptions> options,
    ILogger<DockerService> logger) : IDockerService
{
    private DockerOptions Options => options.Value;

    public async Task<IReadOnlyList<DockerContainerDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Options.Enabled)
        {
            return [];
        }

        var result = await RunAsync(["ps", "-a", "--format", "{{json .}}"], cancellationToken);
        if (!result.Success)
        {
            logger.LogWarning("docker ps failed: {Error}", result.Error);
            if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                result.Error.Contains("cannot connect", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            throw new DomainException($"Docker list failed: {result.Error}");
        }

        var containers = new List<DockerContainerDto>();
        using var reader = new StringReader(result.Output);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var id = root.TryGetProperty("ID", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
                var names = root.TryGetProperty("Names", out var namesProp) ? namesProp.GetString() ?? string.Empty : string.Empty;
                var image = root.TryGetProperty("Image", out var imgProp) ? imgProp.GetString() ?? string.Empty : string.Empty;
                var state = root.TryGetProperty("State", out var stateProp) ? stateProp.GetString() ?? string.Empty : string.Empty;
                var status = root.TryGetProperty("Status", out var statusProp) ? statusProp.GetString() ?? string.Empty : string.Empty;
                var ports = root.TryGetProperty("Ports", out var portsProp) ? portsProp.GetString() : null;
                var createdAtRaw = root.TryGetProperty("CreatedAt", out var createdProp) ? createdProp.GetString() : null;
                var createdAt = ParseCreatedAt(createdAtRaw);

                var name = names.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? names;

                containers.Add(new DockerContainerDto(
                    id,
                    name,
                    image,
                    state.ToLowerInvariant(),
                    status,
                    string.IsNullOrWhiteSpace(ports) ? null : ports,
                    createdAt));
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to parse docker ps line: {Line}", line);
            }
        }

        return containers.OrderBy(c => c.Name).ToArray();
    }

    public async Task<DockerContainerDto?> InspectAsync(string container, CancellationToken cancellationToken = default)
    {
        var name = NormalizeName(container);
        var containers = await ListAsync(cancellationToken);
        return containers.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<DockerActionResultDto> ExecuteAsync(string container, string action, CancellationToken cancellationToken = default)
    {
        if (!Options.Enabled)
        {
            throw new DomainException("Docker control is disabled.");
        }

        var name = NormalizeName(container);
        var normalizedAction = action?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedAction is not ("start" or "stop" or "restart"))
        {
            throw new DomainException("Action must be 'start', 'stop' or 'restart'.");
        }

        var existing = await InspectAsync(name, cancellationToken);
        if (existing is null)
        {
            throw new DomainException($"Container '{name}' was not found.");
        }

        if (normalizedAction == "start" && existing.State == "running")
        {
            return new DockerActionResultDto(name, normalizedAction, true, existing.Status, existing.State, "Already running.");
        }

        if (normalizedAction == "stop" && existing.State != "running")
        {
            return new DockerActionResultDto(name, normalizedAction, true, existing.Status, existing.State, "Already stopped.");
        }

        logger.LogInformation("Docker action {Action} requested for container {Container}", normalizedAction, name);

        var result = await RunAsync([normalizedAction, name], cancellationToken);
        if (!result.Success)
        {
            logger.LogWarning("docker {Action} {Container} failed: {Error}", normalizedAction, name, result.Error);
            throw new DomainException($"Docker {normalizedAction} failed: {result.Error}");
        }

        await Task.Delay(500, cancellationToken);
        var after = await InspectAsync(name, cancellationToken);

        return new DockerActionResultDto(
            name,
            normalizedAction,
            true,
            after?.Status ?? result.Output.Trim(),
            after?.State,
            $"{normalizedAction} succeeded.");
    }

    private static string NormalizeName(string container)
    {
        if (string.IsNullOrWhiteSpace(container))
        {
            throw new DomainException("Container name is required.");
        }

        var trimmed = container.Trim();
        if (!Regex.IsMatch(trimmed, @"^[a-zA-Z0-9][a-zA-Z0-9_.\-]*$"))
        {
            throw new DomainException("Docker container name is invalid.");
        }

        return trimmed;
    }

    private static DateTime ParseCreatedAt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DateTime.UtcNow;
        }

        if (DateTime.TryParse(raw, out var parsed))
        {
            return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
        }

        return DateTime.UtcNow;
    }

    private async Task<(bool Success, string Output, string Error)> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var candidates = GetExecutableCandidates();

        foreach (var exe in candidates)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            try
            {
                if (!process.Start())
                {
                    continue;
                }

                var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(Options.TimeoutSeconds));

                try
                {
                    await process.WaitForExitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return (false, string.Empty, $"docker {string.Join(" ", args)} timed out after {Options.TimeoutSeconds}s.");
                }

                var output = await outputTask;
                var error = await errorTask;

                if (process.ExitCode != 0)
                {
                    var detail = string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim();
                    if (string.IsNullOrWhiteSpace(detail))
                    {
                        detail = $"Exit code {process.ExitCode}";
                    }

                    // If executable not found, try next candidate
                    if (detail.Contains("not recognized", StringComparison.OrdinalIgnoreCase) ||
                        detail.Contains("cannot find the file", StringComparison.OrdinalIgnoreCase) ||
                        detail.Contains("No such file", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogDebug("Docker executable {Exe} not usable, trying fallback: {Detail}", exe, detail);
                        continue;
                    }

                    return (false, output, detail);
                }

                return (true, output, string.Empty);
            }
            catch (Exception ex) when (ex is global::System.ComponentModel.Win32Exception or global::System.IO.FileNotFoundException)
            {
                logger.LogDebug(ex, "Docker executable {Exe} not found, trying fallback", exe);
                continue;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to start docker executable {Exe}", exe);
                return (false, string.Empty, ex.Message);
            }
        }

        return (false, string.Empty, $"Docker executable not found. Tried: {string.Join(", ", candidates)}");
    }

    private IReadOnlyList<string> GetExecutableCandidates()
    {
        var primary = Options.ExecutablePath?.Trim();
        if (string.IsNullOrWhiteSpace(primary))
        {
            primary = "docker";
        }

        var list = new List<string> { primary };

        // Common Windows install location for Docker Desktop when running as service (LocalSystem PATH may not include it)
        var common = new[]
        {
            @"C:\Program Files\Docker\Docker\resources\bin\docker.exe",
            @"C:\Program Files\Docker\Docker\resources\bin\docker",
        };

        foreach (var c in common)
        {
            if (!list.Contains(c, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(c);
            }
        }

        if (!list.Contains("docker.exe", StringComparer.OrdinalIgnoreCase))
        {
            list.Add("docker.exe");
        }

        return list;
    }
}
