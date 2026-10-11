namespace MyHomeLab.Infrastructure.Terminal;

public sealed class TerminalOptions
{
    public bool Enabled { get; set; } = true;

    public string DefaultShell { get; set; } = "powershell";

    public string[] AllowedShells { get; set; } = ["powershell", "pwsh", "cmd"];

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxOutputBytes { get; set; } = 100_000;

    public string? DefaultWorkingDirectory { get; set; }

    public bool PtyEnabled { get; set; } = true;

    public int PtyMaxSessions { get; set; } = 4;

    public int PtyIdleTimeoutSeconds { get; set; } = 600;

    public int PtyMaxLifetimeSeconds { get; set; } = 3600;
}
