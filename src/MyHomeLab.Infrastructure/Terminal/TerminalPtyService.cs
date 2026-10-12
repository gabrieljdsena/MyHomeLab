using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Domain;

namespace MyHomeLab.Infrastructure.Terminal;

public sealed class TerminalPtyService(
    IOptions<TerminalOptions> options,
    IToolchainService toolchain,
    ILogger<TerminalPtyService> logger) : ITerminalPtyService
{
    private readonly TerminalOptions config = options.Value;
    private int activeSessions;

    public TerminalPtyConfig GetPtyConfig()
    {
        var allowed = config.AllowedShells
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (allowed.Length == 0)
        {
            allowed = ["powershell", "pwsh", "cmd"];
        }

        return new TerminalPtyConfig(
            config.Enabled && config.PtyEnabled,
            config.DefaultShell,
            allowed,
            ResolveDefaultWorkingDirectory(),
            Math.Clamp(config.PtyMaxSessions, 1, 16),
            Math.Clamp(config.PtyIdleTimeoutSeconds, 60, 7200),
            Math.Clamp(config.PtyMaxLifetimeSeconds, 300, 14400));
    }

    public Task<ITerminalPtySession> CreateSessionAsync(
        string? shell,
        string? cwd,
        int cols,
        int rows,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new DomainException("Interactive terminal requires Windows.");
        }

        var pty = GetPtyConfig();
        if (!pty.Enabled)
        {
            throw new DomainException("Interactive terminal is disabled on this host.");
        }

        var resolvedShell = (shell ?? pty.DefaultShell).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(resolvedShell))
        {
            resolvedShell = pty.DefaultShell.ToLowerInvariant();
        }

        if (!pty.AllowedShells.Contains(resolvedShell, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainException($"Shell '{resolvedShell}' is not allowed. Allowed: {string.Join(", ", pty.AllowedShells)}.");
        }

        var directory = ResolveWorkingDirectory(cwd);

        var claimed = Interlocked.Increment(ref activeSessions);
        if (claimed > pty.MaxSessions)
        {
            Interlocked.Decrement(ref activeSessions);
            throw new DomainException($"Too many terminal sessions (max {pty.MaxSessions}). Close one and retry.");
        }

        NodePtySession session;
        try
        {
            session = NodePtySession.Create(
                ResolveNodeExecutable(),
                ResolveBridgeDirectory(),
                resolvedShell,
                directory,
                cols,
                rows,
                TimeSpan.FromSeconds(pty.IdleTimeoutSeconds),
                TimeSpan.FromSeconds(pty.MaxLifetimeSeconds));
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            Interlocked.Decrement(ref activeSessions);
            logger.LogError(ex, "Failed to start PTY session shell={Shell} cwd={Cwd}", resolvedShell, directory);
            throw new DomainException($"Failed to start terminal session: {ex.Message}");
        }

        logger.LogInformation(
            "Terminal PTY session started id={Id} shell={Shell} cwd={Cwd}",
            session.Id, resolvedShell, directory);

        return Task.FromResult<ITerminalPtySession>(new CountingSession(session, OnSessionClosed));
    }

    private void OnSessionClosed(Guid id)
    {
        Interlocked.Decrement(ref activeSessions);
        logger.LogInformation("Terminal PTY session closed id={Id}", id);
    }

    private string ResolveNodeExecutable()
    {
        return toolchain.ResolvePath("node")
            ?? (OperatingSystem.IsWindows() ? "node.exe" : "node");
    }

    private static string ResolveBridgeDirectory()
    {
        var published = Path.Combine(AppContext.BaseDirectory, "pty-bridge");
        if (File.Exists(Path.Combine(published, "pty-bridge.js")))
        {
            return published;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 8 && dir is not null; depth++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "MyHomeLab.Infrastructure", "Terminal", "NodePty", "pty-bridge");
            if (File.Exists(Path.Combine(candidate, "pty-bridge.js")))
            {
                return candidate;
            }
        }

        return published;
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

    private sealed class CountingSession(NodePtySession inner, Action<Guid> onClosed) : ITerminalPtySession
    {
        private int closed;

        public Guid Id => inner.Id;

        public int? ExitCode => inner.ExitCode;

        public string? EndReason => inner.EndReason;

        public Task<string?> ReadOutputAsync(CancellationToken cancellationToken) =>
            inner.ReadOutputAsync(cancellationToken);

        public Task WriteInputAsync(string data, CancellationToken cancellationToken) =>
            inner.WriteInputAsync(data, cancellationToken);

        public void Resize(int cols, int rows) => inner.Resize(cols, rows);

        public async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            if (Interlocked.Exchange(ref closed, 1) == 0)
            {
                onClosed(inner.Id);
            }
        }
    }
}
