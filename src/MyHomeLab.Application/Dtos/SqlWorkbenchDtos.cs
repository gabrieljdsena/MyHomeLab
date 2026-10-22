namespace MyHomeLab.Application.Dtos;

public sealed record SqlWorkbenchRequest(string? Sql, string? Database);

public sealed record SqlWorkbenchResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int RowCount,
    string Command,
    long DurationMs,
    bool Truncated);
