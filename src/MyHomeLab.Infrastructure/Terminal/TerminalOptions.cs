namespace MyHomeLab.Infrastructure.Terminal;

public sealed class TerminalOptions
{
    public bool Enabled { get; set; } = true;

    public string DefaultShell { get; set; } = "powershell";

    public string[] AllowedShells { get; set; } = ["powershell", "pwsh", "cmd"];

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxOutputBytes { get; set; } = 100_000;

    public string? DefaultWorkingDirectory { get; set; }
}
