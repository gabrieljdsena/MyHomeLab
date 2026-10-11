using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Application.Abstractions;

public interface ITerminalPtySession : IAsyncDisposable
{
    Guid Id { get; }

    int? ExitCode { get; }

    string? EndReason { get; }

    Task<string?> ReadOutputAsync(CancellationToken cancellationToken);

    Task WriteInputAsync(string data, CancellationToken cancellationToken);

    void Resize(int cols, int rows);
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
}
