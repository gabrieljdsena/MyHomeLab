using Microsoft.Extensions.Options;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Errors;
using Npgsql;

namespace MyHomeLab.Infrastructure.Persistence;

public sealed class NpgsqlSqlExecutor(
    NpgsqlDataSource dataSource,
    NpgsqlConnectionSource connectionSource,
    IOptions<PostgresWorkbenchOptions> options) : ISqlExecutor
{
    private const int FallbackTimeoutSeconds = 10;
    private const int FallbackMaxRows = 500;

    public async Task<SqlExecution> ExecuteAsync(string sql, string? database = null, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var timeoutSeconds = settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : FallbackTimeoutSeconds;
        var maxRows = settings.MaxRows > 0 ? settings.MaxRows : FallbackMaxRows;

        try
        {
            await using var connection = await OpenConnectionAsync(database, cancellationToken);
            await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = timeoutSeconds };
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var columns = new List<string>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            var rows = new List<IReadOnlyList<object?>>();
            var truncated = false;
            while (await reader.ReadAsync(cancellationToken))
            {
                if (rows.Count >= maxRows)
                {
                    truncated = true;
                    break;
                }

                var row = new object?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = Normalize(reader.GetValue(i));
                }

                rows.Add(row);
            }

            var affected = reader.RecordsAffected;
            return new SqlExecution(
                columns,
                rows,
                columns.Count > 0 ? rows.Count : Math.Max(affected, 0),
                truncated);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.QueryCanceled)
        {
            throw new SqlExecutionException($"Statement exceeded the {timeoutSeconds}s time limit.");
        }
        catch (PostgresException ex)
        {
            throw new SqlExecutionException(ex.MessageText);
        }
        catch (NpgsqlException ex) when (ex.InnerException is TimeoutException)
        {
            throw new SqlExecutionException($"Statement exceeded the {timeoutSeconds}s time limit.");
        }
        catch (NpgsqlException ex)
        {
            throw new SqlExecutionException(ex.Message);
        }
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync(string? database, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(database))
        {
            return await dataSource.OpenConnectionAsync(cancellationToken);
        }

        // Postgres has no cross-database queries, so targeting another database means
        // opening a one-off connection to it with the hub's own credentials.
        var connection = new NpgsqlConnection(connectionSource.ForDatabase(database));
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        return connection;
    }

    // The grid only renders JSON primitives: map everything else to display text,
    // and keep byte[] out of the payload (base64 would dwarf the result).
    private static object? Normalize(object? value) => value switch
    {
        null or DBNull => null,
        bool or byte or short or int or long or float or double or decimal or string => value,
        DateTime or DateTimeOffset => value,
        Guid guid => guid.ToString(),
        TimeSpan timeSpan => timeSpan.ToString(),
        byte[] bytes => $"{bytes.Length} bytes",
        Array array => string.Join(", ", array.Cast<object?>().Select(item => item?.ToString() ?? "NULL")),
        _ => value.ToString(),
    };
}
