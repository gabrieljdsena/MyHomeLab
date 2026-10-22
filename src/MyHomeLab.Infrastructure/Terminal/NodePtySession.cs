using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MyHomeLab.Application.Abstractions;

namespace MyHomeLab.Infrastructure.Terminal;

public sealed class NodePtySession : ITerminalPtySession
{
    private const int MaxReplayChars = 512 * 1024;
    private const int SubscriberQueueCapacity = 1024;

    private readonly Process process;
    private readonly object gate = new();
    private readonly List<Channel<string>> subscribers = [];
    private readonly Queue<string> replayBuffer = new();
    private readonly CancellationTokenSource pumpCts = new();
    private readonly Task pumpTask;
    private readonly Task watchTask;
    private readonly Timer watchdog;
    private readonly TimeSpan idleTimeout;
    private readonly TimeSpan maxLifetime;
    private readonly DateTime startedAtUtc = DateTime.UtcNow;
    private long lastActivityTicks = DateTime.UtcNow.Ticks;
    private int replayChars;
    private int ended;
    private int disposed;

    public event Action<Guid>? Ended;

    public Guid Id { get; } = Guid.NewGuid();

    public string Shell { get; }

    public string WorkingDirectory { get; }

    public DateTime StartedAtUtc => startedAtUtc;

    public DateTime LastActivityUtc => new(Interlocked.Read(ref lastActivityTicks), DateTimeKind.Utc);

    public int AttachedClients
    {
        get
        {
            lock (gate)
            {
                return subscribers.Count;
            }
        }
    }

    public bool HasEnded => Volatile.Read(ref ended) != 0;

    private int? processExitCode;

    private int? shellExitCode;

    public int? ExitCode => shellExitCode ?? processExitCode;

    public string? EndReason { get; private set; }

    private NodePtySession(
        Process process,
        string shell,
        string workingDirectory,
        TimeSpan idleTimeout,
        TimeSpan maxLifetime)
    {
        this.process = process;
        this.Shell = shell;
        this.WorkingDirectory = workingDirectory;
        this.idleTimeout = idleTimeout;
        this.maxLifetime = maxLifetime;
        pumpTask = PumpAsync(pumpCts.Token);
        watchTask = WatchAsync();
        _ = DrainStderrAsync(process, pumpCts.Token);
        watchdog = new Timer(OnWatchdog, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
    }

    public static NodePtySession Create(
        string nodeExecutable,
        string bridgeDirectory,
        string commandLineShell,
        string workingDirectory,
        int cols,
        int rows,
        TimeSpan idleTimeout,
        TimeSpan maxLifetime)
    {
        cols = Math.Clamp(cols, 20, 500);
        rows = Math.Clamp(rows, 5, 200);

        var bridgeJs = Path.Combine(bridgeDirectory, "pty-bridge.js");
        if (!File.Exists(bridgeJs))
        {
            throw new InvalidOperationException($"PTY bridge not found: {bridgeJs}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = nodeExecutable,
            WorkingDirectory = bridgeDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("pty-bridge.js");
        psi.ArgumentList.Add("--shell");
        psi.ArgumentList.Add(commandLineShell);
        psi.ArgumentList.Add("--cwd");
        psi.ArgumentList.Add(workingDirectory);
        psi.ArgumentList.Add("--cols");
        psi.ArgumentList.Add(cols.ToString());
        psi.ArgumentList.Add("--rows");
        psi.ArgumentList.Add(rows.ToString());

        Process? child;
        try
        {
            child = Process.Start(psi);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start PTY bridge '{nodeExecutable}': {ex.Message}", ex);
        }

        if (child is null)
        {
            throw new InvalidOperationException("Failed to start PTY bridge process.");
        }

        return new NodePtySession(child, commandLineShell, workingDirectory, idleTimeout, maxLifetime);
    }

    public ITerminalPtyAttachment Attach()
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(SubscriberQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        lock (gate)
        {
            foreach (var chunk in replayBuffer)
            {
                channel.Writer.TryWrite(chunk);
            }

            if (ended != 0)
            {
                channel.Writer.TryComplete();
            }
            else
            {
                subscribers.Add(channel);
            }
        }

        return new Attachment(this, channel);
    }

    public async Task WriteInputAsync(string data, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        if (string.IsNullOrEmpty(data))
        {
            return;
        }

        var frame = JsonSerializer.Serialize(new { type = "input", data }) + "\n";
        try
        {
            await process.StandardInput.WriteAsync(frame.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Terminal session input is closed: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Terminal session input failed: {ex.Message}", ex);
        }

        Touch();
    }

    public void Resize(int cols, int rows)
    {
        if (disposed != 0)
        {
            return;
        }

        var frame = JsonSerializer.Serialize(new
        {
            type = "resize",
            cols = Math.Clamp(cols, 20, 500),
            rows = Math.Clamp(rows, 5, 200),
        }) + "\n";
        try
        {
            process.StandardInput.Write(frame);
            process.StandardInput.Flush();
        }
        catch
        {
            // Best effort; the client re-sends on every fit.
        }

        Touch();
    }

    public void MarkEndReason(string reason)
    {
        EndReason ??= reason;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await watchdog.DisposeAsync();
        }
        catch
        {
            // ignore
        }

        try
        {
            await pumpCts.CancelAsync();
        }
        catch
        {
            // ignore
        }

        if (!process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }
        }

        try
        {
            await Task.WhenAny(Task.WhenAll(pumpTask, watchTask), Task.Delay(TimeSpan.FromSeconds(5)));
        }
        catch
        {
            // ignore
        }

        End();

        try
        {
            process.Dispose();
        }
        catch
        {
            // ignore
        }

        pumpCts.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Detach(Channel<string> channel)
    {
        lock (gate)
        {
            subscribers.Remove(channel);
        }
    }

    private void Publish(string chunk)
    {
        lock (gate)
        {
            replayBuffer.Enqueue(chunk);
            replayChars += chunk.Length;
            while (replayChars > MaxReplayChars && replayBuffer.Count > 1)
            {
                replayChars -= replayBuffer.Dequeue().Length;
            }

            foreach (var subscriber in subscribers)
            {
                subscriber.Writer.TryWrite(chunk);
            }
        }
    }

    private void End()
    {
        if (Interlocked.Exchange(ref ended, 1) != 0)
        {
            return;
        }

        lock (gate)
        {
            foreach (var subscriber in subscribers)
            {
                subscriber.Writer.TryComplete();
            }

            subscribers.Clear();
        }

        Ended?.Invoke(Id);
    }

    private async Task PumpAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                Touch();
                var frame = ParseBridgeFrame(line);
                if (frame is null)
                {
                    continue;
                }

                if (frame.Ended)
                {
                    shellExitCode = frame.ExitCode;
                    break;
                }

                if (frame.Output is not null)
                {
                    Publish(frame.Output);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch (ObjectDisposedException)
        {
            // Shutting down.
        }
        catch (IOException)
        {
            // Bridge went away.
        }
        catch (Exception)
        {
            // Never fail unpaired; the exit path publishes end of stream.
        }
        finally
        {
            End();
        }
    }

    private async Task WatchAsync()
    {
        try
        {
            await process.WaitForExitAsync();
            processExitCode = process.ExitCode;
        }
        catch
        {
            processExitCode = -1;
        }
        finally
        {
            try
            {
                await Task.WhenAny(pumpTask, Task.Delay(TimeSpan.FromSeconds(2)));
            }
            catch
            {
                // ignore
            }

            End();
        }
    }

    private void OnWatchdog(object? _)
    {
        var now = DateTime.UtcNow;
        var idle = now - new DateTime(Interlocked.Read(ref lastActivityTicks), DateTimeKind.Utc);
        if (idle >= idleTimeout)
        {
            MarkEndReason($"Session closed after {idleTimeout.TotalMinutes:F0} minutes idle.");
            _ = DisposeAsync();
        }
        else if (now - startedAtUtc >= maxLifetime)
        {
            MarkEndReason($"Session closed after exceeding the {maxLifetime.TotalMinutes:F0} minute lifetime.");
            _ = DisposeAsync();
        }
    }

    private void Touch() => Interlocked.Exchange(ref lastActivityTicks, DateTime.UtcNow.Ticks);

    private static async Task DrainStderrAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync(cancellationToken) is not null)
            {
                // Discard bridge/node diagnostics so a full stderr pipe can never block the bridge.
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch (ObjectDisposedException)
        {
            // Shutting down.
        }
        catch (IOException)
        {
            // Bridge went away.
        }
        catch (Exception)
        {
            // Drain is best effort only.
        }
    }

    private sealed class Attachment(NodePtySession session, Channel<string> channel) : ITerminalPtyAttachment
    {
        private int disposed;

        public async Task<string?> ReadOutputAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await channel.Reader.ReadAsync(cancellationToken);
            }
            catch (ChannelClosedException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                session.Detach(channel);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed record BridgeFrame(string? Output, bool Ended, int ExitCode);

    private static BridgeFrame? ParseBridgeFrame(string line)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!doc.RootElement.TryGetProperty("type", out var typeProp))
            {
                return null;
            }

            var type = typeProp.GetString();
            if (type == "output"
                && doc.RootElement.TryGetProperty("data", out var dataProp)
                && dataProp.ValueKind == JsonValueKind.String)
            {
                return new BridgeFrame(dataProp.GetString(), false, 0);
            }

            if (type == "exit")
            {
                var code = -1;
                if (doc.RootElement.TryGetProperty("code", out var codeProp)
                    && codeProp.ValueKind == JsonValueKind.Number
                    && codeProp.TryGetInt32(out var parsed))
                {
                    code = parsed;
                }

                return new BridgeFrame(null, true, code);
            }

            return null;
        }
    }
}
