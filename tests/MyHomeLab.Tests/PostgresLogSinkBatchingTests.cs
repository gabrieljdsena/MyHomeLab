using System.Threading.Channels;
using MyHomeLab.Infrastructure.Logging;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace MyHomeLab.Tests;

public class PostgresLogSinkBatchingTests
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(50);

    private static LogEvent ErrorEvent(string message) =>
        new(DateTimeOffset.UtcNow, LogEventLevel.Error, null,
            new MessageTemplateParser().Parse(message), []);

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Timed out waiting for {what}.");
            }

            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task Emit_Buffers_And_Flushes_One_Batch()
    {
        var flushed = new List<IReadOnlyList<string>>();
        using var sink = new PostgresLogSink(
            Channel.CreateUnbounded<string>(),
            (batch, _) =>
            {
                lock (flushed)
                {
                    flushed.Add(batch.ToArray());
                }

                return Task.CompletedTask;
            },
            FlushInterval);

        sink.Emit(ErrorEvent("one"));
        sink.Emit(ErrorEvent("two"));

        await WaitUntilAsync(() => flushed.Count >= 1, "batch flush");

        Assert.Single(flushed);
        Assert.Equal(2, flushed[0].Count);
        Assert.Contains("one", flushed[0][0]);
        Assert.Contains("two", flushed[0][1]);
    }

    [Fact]
    public void Dispose_Flushes_Pending_Items()
    {
        var flushed = new List<string>();
        var sink = new PostgresLogSink(
            Channel.CreateUnbounded<string>(),
            (batch, _) =>
            {
                flushed.AddRange(batch);
                return Task.CompletedTask;
            },
            // Long enough that only Dispose triggers the flush.
            TimeSpan.FromMinutes(1));

        sink.Emit(ErrorEvent("one"));
        sink.Dispose();

        Assert.Single(flushed);
        Assert.Contains("one", flushed[0]);
    }

    [Fact]
    public async Task Emit_Drops_Newest_When_Queue_Full()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var flushed = new List<string>();
        using var sink = new PostgresLogSink(
            Channel.CreateBounded<string>(new BoundedChannelOptions(1)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropWrite,
            }),
            async (batch, _) =>
            {
                entered.TrySetResult();
                await release.Task;
                flushed.AddRange(batch);
            },
            FlushInterval);

        sink.Emit(ErrorEvent("first"));
        await WaitUntilAsync(() => entered.Task.IsCompleted, "flusher to block on first write");
        sink.Emit(ErrorEvent("second"));
        sink.Emit(ErrorEvent("dropped"));
        release.TrySetResult();
        sink.Dispose();

        Assert.Equal(2, flushed.Count);
        Assert.Contains("first", flushed[0]);
        Assert.Contains("second", flushed[1]);
    }
}
