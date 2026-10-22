using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Errors;
using MyHomeLab.Application.Services;
using Xunit;

namespace MyHomeLab.Tests;

public class SqlWorkbenchServiceTests
{
    private sealed class FakeSqlExecutor : ISqlExecutor
    {
        public int Calls { get; private set; }
        public string? LastSql { get; private set; }
        public string? LastDatabase { get; private set; }

        public Task<SqlExecution> ExecuteAsync(string sql, string? database = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastSql = sql;
            LastDatabase = database;
            return Task.FromResult(new SqlExecution(["x"], [[1]], 1, false));
        }
    }

    [Fact]
    public async Task ExecuteAsync_MapsCommandAndResult()
    {
        var executor = new FakeSqlExecutor();
        var service = new SqlWorkbenchService(executor);

        var result = await service.ExecuteAsync(new SqlWorkbenchRequest("select 1", null));

        Assert.Equal("SELECT", result.Command);
        Assert.Equal(1, result.RowCount);
        Assert.Equal(["x"], result.Columns);
        Assert.True(result.DurationMs >= 0);
        Assert.Equal(1, executor.Calls);
        Assert.Equal("select 1", executor.LastSql);
    }

    [Fact]
    public async Task ExecuteAsync_NoDatabase_PassesNull()
    {
        var executor = new FakeSqlExecutor();
        var service = new SqlWorkbenchService(executor);

        await service.ExecuteAsync(new SqlWorkbenchRequest("SELECT 1", null));
        Assert.Null(executor.LastDatabase);

        await service.ExecuteAsync(new SqlWorkbenchRequest("SELECT 1", "   "));
        Assert.Null(executor.LastDatabase);
    }

    [Fact]
    public async Task ExecuteAsync_DatabaseName_IsTrimmed()
    {
        var executor = new FakeSqlExecutor();
        var service = new SqlWorkbenchService(executor);

        await service.ExecuteAsync(new SqlWorkbenchRequest("SELECT 1", " myhomelab "));
        Assert.Equal("myhomelab", executor.LastDatabase);
    }

    [Theory]
    [InlineData("bad name")]
    [InlineData("db;drop")]
    [InlineData("1leading")]
    [InlineData("quote\"name")]
    [InlineData("new\nline")]
    public async Task ExecuteAsync_InvalidDatabaseName_Rejects(string database)
    {
        var executor = new FakeSqlExecutor();
        var service = new SqlWorkbenchService(executor);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ExecuteAsync(new SqlWorkbenchRequest("SELECT 1", database)));
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_TooLongDatabaseName_Rejects()
    {
        var executor = new FakeSqlExecutor();
        var service = new SqlWorkbenchService(executor);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ExecuteAsync(new SqlWorkbenchRequest("SELECT 1", new string('a', 64))));
        Assert.Equal(0, executor.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSql_DoesNotReachExecutor()
    {
        var executor = new FakeSqlExecutor();
        var service = new SqlWorkbenchService(executor);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.ExecuteAsync(new SqlWorkbenchRequest("DROP TABLE apps", null)));
        Assert.Equal(0, executor.Calls);
    }
}
