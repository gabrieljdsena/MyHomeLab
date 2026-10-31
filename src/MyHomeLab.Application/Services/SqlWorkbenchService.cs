using System.Diagnostics;
using System.Text.RegularExpressions;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Errors;

namespace MyHomeLab.Application.Services;

public sealed partial class SqlWorkbenchService(ISqlExecutor executor)
{
    private const int MaxDatabaseNameLength = 63;

    public async Task<SqlWorkbenchResult> ExecuteAsync(SqlWorkbenchRequest request, CancellationToken cancellationToken = default)
    {
        var command = SqlStatementValidator.Validate(request.Sql);
        var database = NormalizeDatabase(request.Database);
        var stopwatch = Stopwatch.StartNew();
        var execution = await executor.ExecuteAsync(request.Sql!, database, cancellationToken);
        stopwatch.Stop();

        return new SqlWorkbenchResult(
            execution.Columns,
            execution.Rows,
            execution.RowCount,
            command,
            stopwatch.ElapsedMilliseconds,
            execution.Truncated);
    }

    // The name ends up in a connection string, never in SQL, but keep it to a plain
    // identifier shape so a typo fails with a clear 400 instead of an odd driver error.
    private static string? NormalizeDatabase(string? database)
    {
        if (string.IsNullOrWhiteSpace(database))
        {
            return null;
        }

        var name = database.Trim();
        if (name.Length > MaxDatabaseNameLength || !DatabaseNamePattern().IsMatch(name))
        {
            throw new ValidationException("Invalid database name.");
        }

        return name;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_$-]*$")]
    private static partial Regex DatabaseNamePattern();
}
