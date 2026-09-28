using MyHomeLab.Infrastructure.Logging;
using Xunit;

namespace MyHomeLab.Tests;

public class PostgresLogSinkTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FormatMessage_Without_Exception()
    {
        var text = PostgresLogSink.FormatMessage(Timestamp, "Error", "boom", null);

        Assert.Equal("2026-09-28 12:00:00Z [Error] boom", text);
    }

    [Fact]
    public void FormatMessage_Appends_Exception_On_Next_Line()
    {
        var text = PostgresLogSink.FormatMessage(Timestamp, "Fatal", "boom", "System.Exception: bang");

        Assert.Equal(
            $"2026-09-28 12:00:00Z [Fatal] boom{Environment.NewLine}System.Exception: bang",
            text);
    }
}
