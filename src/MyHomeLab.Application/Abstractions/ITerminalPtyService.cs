using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Application.Abstractions;

public interface ITerminalPtyAttachment : IAsyncDisposable
{
    Task<string?> ReadOutputAsync(CancellationToken cancellationToken);
}

public interface ITerminalPtySession : IAsyncDisposable
{
    Guid Id { get; }

    string Shell { get; }

    string WorkingDirectory { get; }

    DateTime StartedAtUtc { get; }

    DateTime LastActivityUtc { get; }

    int AttachedClients { get; }

    int? ExitCode { get; }

    string? EndReason { get; }

    void MarkEndReason(string reason);

    Task WriteInputAsync(string data, CancellationToken cancellationToken);

    void Resize(int cols, int rows);

    ITerminalPtyAttachment Attach();
}

public interface ITerminalPtyService
{
    TerminalPtyConfig GetPtyConfig();

    Task<ITerminalPtySession> CreateSessionAsync(
        string? shell,
        string? cwd,
        int cols,
        int rows,
        CancellationToken cancellationToken);

    bool TryGetSession(Guid id, out ITerminalPtySession session);

    IReadOnlyList<TerminalSessionSummary> ListSessions();

    Task<bool> KillSessionAsync(Guid id, CancellationToken cancellationToken);
}
