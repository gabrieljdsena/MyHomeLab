namespace MyHomeLab.Application.Abstractions;

public interface ISqlExecutor
{
    /// <summary>
    /// Runs one already-validated SQL statement against the host database (or the named
    /// one) and returns its result set (empty for DML). Implementations must translate
    /// provider failures into <c>SqlExecutionException</c>.
    /// </summary>
    Task<SqlExecution> ExecuteAsync(string sql, string? database = null, CancellationToken cancellationToken = default);
}

public sealed record SqlExecution(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int RowCount,
    bool Truncated);
