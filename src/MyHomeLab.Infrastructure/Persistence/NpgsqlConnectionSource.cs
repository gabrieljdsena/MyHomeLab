using Npgsql;

namespace MyHomeLab.Infrastructure.Persistence;

// NpgsqlDataSource.ConnectionString omits the password (Npgsql redacts it), so
// one-off connections to other databases must copy from this instead.
public sealed class NpgsqlConnectionSource(string connectionString)
{
    private readonly NpgsqlConnectionStringBuilder builder = new(connectionString);

    public string ForDatabase(string database, int? timeoutSeconds = null)
    {
        var copy = new NpgsqlConnectionStringBuilder(builder.ConnectionString) { Database = database };
        if (timeoutSeconds is int seconds)
        {
            copy.Timeout = seconds;
        }

        return copy.ConnectionString;
    }
}
