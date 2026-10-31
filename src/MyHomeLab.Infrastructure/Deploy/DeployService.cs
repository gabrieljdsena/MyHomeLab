using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Domain;

namespace MyHomeLab.Infrastructure.Deploy;

public sealed partial class DeployService(
    IOptions<DeployOptions> options,
    ILogger<DeployService> logger) : IDeployService
{
    private const int StaleGraceSeconds = 300;
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    private DeployOptions Options => options.Value;

    public async Task<DeployStatusDto> StartDeployAsync(
        Guid appId,
        string serviceName,
        string buildScriptPath,
        CancellationToken cancellationToken = default)
    {
        var service = ValidateServiceName(serviceName);
        var script = ValidateScriptPath(buildScriptPath);

        var gate = Gates.GetOrAdd(appId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var existing = await ReadStatusFileAsync(appId, cancellationToken);
            if (existing is not null &&
                IsActive(existing.State) &&
                DateTime.UtcNow - existing.UpdatedAtUtc < TimeSpan.FromSeconds(Options.TimeoutSeconds + StaleGraceSeconds))
            {
                logger.LogInformation("Deploy already running for app {AppId}; returning active status", appId);
                return ToDto(appId, existing);
            }

            Directory.CreateDirectory(GetAppDirectory(appId));

            var startedAt = DateTime.UtcNow;
            await WriteStatusFileAsync(
                appId,
                new StatusFile("queued", "Deploy queued.", null, startedAt, startedAt),
                cancellationToken);
            await File.WriteAllTextAsync(
                WrapperPath(appId),
                BuildWrapperScript(appId, service, script),
                Utf8WithBom,
                cancellationToken);

            try
            {
                SpawnWrapper(appId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start deploy process for app {AppId}", appId);
                await WriteStatusFileAsync(
                    appId,
                    new StatusFile("failed", "Failed to start the deploy process: " + ex.Message, null, startedAt, DateTime.UtcNow),
                    CancellationToken.None);
                throw new DomainException("Failed to start the deploy process.");
            }

            logger.LogInformation("Deploy started for app {AppId} (service {Service}, script {Script})", appId, service, script);
            return new DeployStatusDto(appId, "queued", "Deploy queued.", null, string.Empty, startedAt, startedAt);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<DeployStatusDto?> GetDeployStatusAsync(Guid appId, CancellationToken cancellationToken = default)
    {
        var file = await ReadStatusFileAsync(appId, cancellationToken);
        return file is null ? null : ToDto(appId, file);
    }

    private static bool IsActive(string state) => state is "queued" or "building" or "restarting";

    private DeployStatusDto ToDto(Guid appId, StatusFile file) =>
        new(appId, file.State, file.Message, file.ExitCode, ReadLogTail(appId), file.StartedAtUtc, file.UpdatedAtUtc);

    private static string ValidateServiceName(string? serviceName)
    {
        var trimmed = serviceName?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new DomainException("Service name is required.");
        }

        if (!ServiceNamePattern().IsMatch(trimmed))
        {
            throw new DomainException("Service name is invalid.");
        }

        return trimmed;
    }

    private static string ValidateScriptPath(string? buildScriptPath)
    {
        var trimmed = buildScriptPath?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new DomainException("Build script path is required.");
        }

        if (!File.Exists(trimmed))
        {
            throw new DomainException($"Build script was not found: {trimmed}");
        }

        return trimmed;
    }

    private string GetAppDirectory(Guid appId) => Path.Combine(ResolveStateRoot(), appId.ToString("N"));

    private string ResolveStateRoot()
    {
        var configured = Options.StateDirectory;
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "myhomelab-deploys")
            : configured;
    }

    private string StatusPath(Guid appId) => Path.Combine(GetAppDirectory(appId), "status.json");

    private string LogPath(Guid appId) => Path.Combine(GetAppDirectory(appId), "deploy.log");

    private string WrapperPath(Guid appId) => Path.Combine(GetAppDirectory(appId), "deploy.ps1");

    private async Task<StatusFile?> ReadStatusFileAsync(Guid appId, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                StatusPath(appId),
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return await JsonSerializer.DeserializeAsync<StatusFile>(stream, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogDebug(ex, "No readable deploy status for app {AppId}", appId);
            return null;
        }
    }

    private async Task WriteStatusFileAsync(Guid appId, StatusFile status, CancellationToken cancellationToken)
    {
        var path = StatusPath(appId);
        var tmp = path + ".tmp";
        await using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, status, cancellationToken: cancellationToken);
        }

        File.Move(tmp, path, overwrite: true);
    }

    private string ReadLogTail(Guid appId)
    {
        var max = Math.Max(1, Options.OutputTailChars);
        try
        {
            using var stream = new FileStream(
                LogPath(appId),
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length == 0)
            {
                return string.Empty;
            }

            var start = Math.Max(0, stream.Length - ((long)max * 2));
            stream.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = reader.ReadToEnd();
            return text.Length <= max ? text : text[^max..];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "No readable deploy log for app {AppId}", appId);
            return string.Empty;
        }
    }

    private void SpawnWrapper(Guid appId)
    {
        var arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{WrapperPath(appId)}\"";
        Exception? lastError = null;

        foreach (var exe in GetPowerShellCandidates())
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = GetAppDirectory(appId),
            };

            try
            {
                using var process = Process.Start(psi);
                if (process is not null)
                {
                    logger.LogDebug("Deploy wrapper for app {AppId} spawned via {Exe}", appId, exe);
                    return;
                }
            }
            catch (Exception ex) when (ex is global::System.ComponentModel.Win32Exception or FileNotFoundException)
            {
                lastError = ex;
                logger.LogDebug(ex, "PowerShell executable {Exe} not usable, trying fallback", exe);
            }
        }

        throw lastError ?? new DomainException("PowerShell executable was not found.");
    }

    private static IReadOnlyList<string> GetPowerShellCandidates()
    {
        var list = new List<string> { "powershell.exe" };

        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(systemRoot))
        {
            list.Add(Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe"));
        }

        list.Add("pwsh.exe");
        return list;
    }

    private string BuildWrapperScript(Guid appId, string service, string script) =>
        WrapperTemplate
            .Replace("__STATUS_PATH__", PsQuote(StatusPath(appId)))
            .Replace("__LOG_PATH__", PsQuote(LogPath(appId)))
            .Replace("__SERVICE_NAME__", PsQuote(service))
            .Replace("__BUILD_SCRIPT_PATH__", PsQuote(script));

    private static string PsQuote(string value) => "'" + value.Replace("'", "''") + "'";

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9 _.\-]*$")]
    private static partial Regex ServiceNamePattern();

    private const string WrapperTemplate = """
        $ErrorActionPreference = 'Continue'
        $PSDefaultParameterValues['Out-File:Encoding'] = 'utf8'
        $statusPath = __STATUS_PATH__
        $logPath = __LOG_PATH__
        $serviceName = __SERVICE_NAME__
        $buildScriptPath = __BUILD_SCRIPT_PATH__
        $startedAt = (Get-Date).ToUniversalTime().ToString('o')

        function Write-Status([string]$state, [string]$message, $exitCode) {
            $payload = [ordered]@{
                state        = $state
                message      = $message
                exitCode     = $exitCode
                startedAtUtc = $startedAt
                updatedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
            }
            $tmp = "$statusPath.tmp"
            $payload | ConvertTo-Json -Compress | Set-Content -LiteralPath $tmp -Encoding UTF8 -Force
            Move-Item -LiteralPath $tmp -Destination $statusPath -Force
        }

        function Try-StartService([string]$reason) {
            try {
                $svc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
                if ($svc -and $svc.Status -ne 'Running') { Start-Service -Name $serviceName -ErrorAction Stop }
            } catch {
                Add-Content -LiteralPath $logPath -Encoding UTF8 -Value ("[deploy] " + $reason + ": " + $_.Exception.Message)
            }
        }

        Set-Location -LiteralPath (Split-Path -Parent $buildScriptPath)
        Write-Status 'building' ("Running " + $buildScriptPath + "...") $null

        $watch = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            & $buildScriptPath *> $logPath
            $exitCode = $LASTEXITCODE
        } catch {
            $exitCode = 1
            Add-Content -LiteralPath $logPath -Encoding UTF8 -Value ("[deploy] " + $_.Exception.Message)
        }
        $watch.Stop()
        if ($null -eq $exitCode) { $exitCode = 0 }

        if ($exitCode -ne 0) {
            Add-Content -LiteralPath $logPath -Encoding UTF8 -Value ("[deploy] build failed (exit code " + $exitCode + ") after " + [math]::Round($watch.Elapsed.TotalSeconds) + "s")
            Try-StartService 'auto-start after failed build'
            Write-Status 'failed' ("Build failed (exit code " + $exitCode + ") after " + [math]::Round($watch.Elapsed.TotalSeconds) + "s.") $exitCode
            exit $exitCode
        }

        Write-Status 'restarting' ("Build succeeded in " + [math]::Round($watch.Elapsed.TotalSeconds) + "s; restarting service '" + $serviceName + "'...") $exitCode

        try {
            $svc = Get-Service -Name $serviceName -ErrorAction Stop
            if ($svc.Status -eq 'Running') { Restart-Service -Name $serviceName -Force -ErrorAction Stop }
            else { Start-Service -Name $serviceName -ErrorAction Stop }
            Write-Status 'done' ("Deployed successfully in " + [math]::Round($watch.Elapsed.TotalSeconds) + "s.") $exitCode
        } catch {
            Add-Content -LiteralPath $logPath -Encoding UTF8 -Value ("[deploy] restart failed: " + $_.Exception.Message)
            Try-StartService 'auto-start after failed restart'
            Write-Status 'failed' ("Build succeeded but restarting service '" + $serviceName + "' failed: " + $_.Exception.Message) $exitCode
            exit 1
        }
        """;

    private sealed record StatusFile(
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("exitCode")] int? ExitCode,
        [property: JsonPropertyName("startedAtUtc")] DateTime StartedAtUtc,
        [property: JsonPropertyName("updatedAtUtc")] DateTime UpdatedAtUtc);
}
