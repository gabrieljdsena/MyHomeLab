using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;
using Xunit;

namespace MyHomeLab.Tests;

public class LogTests
{
    [Fact]
    public void Create_Trims_Application()
    {
        var entry = LogEntry.Create("  api  ", "boom");

        Assert.Equal("api", entry.Application);
        Assert.Equal("boom", entry.Log);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_Rejects_Empty_Application(string? application)
    {
        var ex = Assert.Throws<DomainException>(() =>
            LogEntry.Create(application!, "boom"));

        Assert.Equal("Application is required.", ex.Message);
    }

    [Fact]
    public void Create_Rejects_Application_Over_255_Chars()
    {
        var ex = Assert.Throws<DomainException>(() =>
            LogEntry.Create(new string('a', 256), "boom"));

        Assert.Equal("Application cannot exceed 255 characters.", ex.Message);
    }

    [Fact]
    public void Create_Defaults_Null_Log_To_Empty()
    {
        var entry = LogEntry.Create("api", null!);

        Assert.Equal(string.Empty, entry.Log);
    }
}
