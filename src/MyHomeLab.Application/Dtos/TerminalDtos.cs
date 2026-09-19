namespace MyHomeLab.Application.Dtos;

public record TerminalExecuteRequest(
    string Command,
    string? Cwd,
    string? Shell);

public record TerminalExecuteResponse(
    string Command,
    string Shell,
    string Cwd,
    string ResolvedCwd,
    string Output,
    int ExitCode,
    long DurationMs,
    bool TimedOut,
    DateTime ExecutedAtUtc);

public record TerminalConfigDto(
    bool Enabled,
    string DefaultShell,
    IReadOnlyList<string> AllowedShells,
    int TimeoutSeconds,
    int MaxOutputBytes,
    string DefaultWorkingDirectory);
