using System.Reflection;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MyHomeLab.Infrastructure.Migrations;

public interface IMigrationRunner
{
    Task ApplyAsync(CancellationToken cancellationToken = default);
}

internal sealed class SqlMigrationRunner(NpgsqlDataSource dataSource, ILogger<SqlMigrationRunner> logger) : IMigrationRunner
{
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS schema_migrations (
                id         INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                version    TEXT NOT NULL UNIQUE,
                applied_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            """);

        var appliedVersions = (await connection.QueryAsync<string>(
            "SELECT version FROM schema_migrations")).ToHashSet(StringComparer.Ordinal);

        var migrations = GetMigrations()
            .Where(m => !appliedVersions.Contains(m.Version))
            .OrderBy(m => m.SortKey, StringComparer.Ordinal)
            .ToArray();

        if (migrations.Length == 0)
        {
            return;
        }

        logger.LogInformation("Applying {Count} pending database migration(s).", migrations.Length);

        foreach (var migration in migrations)
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await connection.ExecuteAsync(migration.Script, transaction: transaction);
            await connection.ExecuteAsync(
                "INSERT INTO schema_migrations (version) VALUES (@Version)",
                new { migration.Version },
                transaction: transaction);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Applied migration {Version}.", migration.Version);
        }
    }

    private static IReadOnlyList<Migration> GetMigrations()
    {
        var assembly = typeof(SqlMigrationRunner).Assembly;
        var prefix = assembly.GetName().Name + ".Migrations.Scripts.";

        return assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.Ordinal))
            .Select(name =>
            {
                var version = name[prefix.Length..^".sql".Length];
                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"Embedded migration '{name}' could not be read.");
                using var reader = new StreamReader(stream);
                return new Migration(version, version, reader.ReadToEnd());
            })
            .ToArray();
    }

    private sealed record Migration(string Version, string SortKey, string Script);
}