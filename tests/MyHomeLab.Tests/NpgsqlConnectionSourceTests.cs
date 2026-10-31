using MyHomeLab.Infrastructure.Persistence;
using Npgsql;

namespace MyHomeLab.Tests;

public sealed class NpgsqlConnectionSourceTests
{
    [Fact]
    public void KeepsCredentialsWhenTargetingAnotherDatabase()
    {
        var source = new NpgsqlConnectionSource(
            "Host=localhost;Port=5432;Database=hub;Username=postgres;Password=topsecret123");

        var builder = new NpgsqlConnectionStringBuilder(source.ForDatabase("other", 5));

        Assert.Equal("other", builder.Database);
        Assert.Equal("topsecret123", builder.Password);
        Assert.Equal("postgres", builder.Username);
        Assert.Equal(5, builder.Timeout);
    }

    [Fact]
    public void PreservesConfiguredTimeoutWhenNoneRequested()
    {
        var source = new NpgsqlConnectionSource(
            "Host=localhost;Port=5432;Database=hub;Username=postgres;Password=topsecret123;Timeout=30");

        var builder = new NpgsqlConnectionStringBuilder(source.ForDatabase("other"));

        Assert.Equal(30, builder.Timeout);
    }

    [Fact]
    public void DataSourceRedactsPasswordSoSourceMustNotBeDerivedFromIt()
    {
        var dataSource = NpgsqlDataSource.Create(
            "Host=localhost;Port=5432;Database=hub;Username=postgres;Password=topsecret123");
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);

            Assert.True(string.IsNullOrEmpty(builder.Password));
        }
        finally
        {
            dataSource.Dispose();
        }
    }
}
