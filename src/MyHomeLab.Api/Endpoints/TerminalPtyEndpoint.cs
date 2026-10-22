using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MyHomeLab.Api.Auth;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Domain;

namespace MyHomeLab.Api.Endpoints;

public static class TerminalPtyEndpoint
{
    private const int MaxInputChars = 65536;

    public static void MapTerminalPty(this WebApplication app)
    {
        app.MapGet("/api/terminal/ws", async (
            HttpContext context,
            ITerminalPtyService pty,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            CancellationToken requestAborted) =>
        {
            var logger = loggerFactory.CreateLogger("TerminalPty");

            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("WebSocket request expected.", requestAborted);
                return;
            }

            if (!OriginPolicy.IsAllowed(context, configuration))
            {
                logger.LogWarning(
                    "Rejected cross-site terminal WebSocket handshake from origin '{Origin}'",
                    context.Request.Headers.Origin.ToString());
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Cross-site WebSocket handshake rejected.", requestAborted);
                return;
            }

            var query = context.Request.Query;
            var shell = query["shell"].FirstOrDefault();
            var cwd = query["cwd"].FirstOrDefault();
            var sessionParam = query["session"].FirstOrDefault();
            var cols = ParseSize(query["cols"].FirstOrDefault(), 120, 20, 500);
            var rows = ParseSize(query["rows"].FirstOrDefault(), 30, 5, 200);

            Guid? requestedSession = null;
            ITerminalPtySession? session = null;
            string? rejection = null;

            if (!string.IsNullOrWhiteSpace(sessionParam))
            {
                if (!Guid.TryParse(sessionParam, out var parsed))
                {
                    rejection = "Invalid terminal session id.";
                }
                else if (pty.TryGetSession(parsed, out session))
                {
                    requestedSession = parsed;
                }
                else
                {
                    rejection = "This terminal session no longer exists. Start a new one.";
                }
            }
            else
            {
                try
                {
                    session = await pty.CreateSessionAsync(shell, cwd, cols, rows, requestAborted);
                }
                catch (DomainException ex)
                {
                    rejection = ex.Message;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Terminal PTY session creation failed unexpectedly.");
                    rejection = "Failed to start terminal session.";
                }
            }

            if (session is null)
            {
                logger.LogWarning(
                    "Terminal PTY session rejected for {Remote}: {Reason}",
                    context.Connection.RemoteIpAddress, rejection);
                await RejectAsync(context, rejection ?? "Terminal is unavailable.", logger, requestAborted);
                return;
            }

            WebSocket ws;
            try
            {
                ws = await context.WebSockets.AcceptWebSocketAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Terminal PTY WebSocket accept failed.");
                return;
            }

            using (ws)
            {
                await SendFrameAsync(
                    ws,
                    new { type = "attached", session = session.Id, reattached = requestedSession is not null },
                    CancellationToken.None);
                await BridgeAsync(ws, session, logger, requestAborted);
            }
        }).RequireAuthorization();
    }

    private static async Task RejectAsync(
        HttpContext context,
        string reason,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        WebSocket? ws = null;
        try
        {
            ws = await context.WebSockets.AcceptWebSocketAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Terminal PTY WebSocket accept failed while rejecting session.");
            return;
        }

        using (ws)
        {
            try
            {
                var frame = JsonSerializer.SerializeToUtf8Bytes(new { type = "error", message = reason });
                await ws.SendAsync(frame, WebSocketMessageType.Text, true, cancellationToken);
                var description = reason.Length > 100 ? reason[..100] : reason;
                await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, description, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Terminal PTY rejection send failed.");
            }
        }
    }

    private static async Task BridgeAsync(
        WebSocket ws,
        ITerminalPtySession session,
        ILogger logger,
        CancellationToken requestAborted)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        await using var attachment = session.Attach();
        var receiveTask = ReceiveLoopAsync(ws, session, cts);
        var sendTask = SendLoopAsync(ws, attachment, session, logger, cts.Token);

        await Task.WhenAny(receiveTask, sendTask);
        cts.Cancel();

        try
        {
            await Task.WhenAll(receiveTask, sendTask);
        }
        catch (OperationCanceledException)
        {
            // Expected on teardown.
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Terminal PTY bridge teardown hit an error.");
        }

        try
        {
            if (ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "session ended", CancellationToken.None);
            }
        }
        catch
        {
            // Client already gone.
        }
    }

    private static async Task ReceiveLoopAsync(WebSocket ws, ITerminalPtySession session, CancellationTokenSource cts)
    {
        var buffer = new byte[8192];
        using var message = new MemoryStream();
        while (!cts.Token.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(buffer, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
            {
                continue;
            }

            var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            message.SetLength(0);

            try
            {
                await HandleClientMessageAsync(session, json, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Malformed frame or torn-down session; keep the socket alive.
            }
        }
    }

    private static async Task HandleClientMessageAsync(ITerminalPtySession session, string json, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!doc.RootElement.TryGetProperty("type", out var typeProp))
        {
            return;
        }

        switch (typeProp.GetString())
        {
            case "input":
                if (doc.RootElement.TryGetProperty("data", out var dataProp)
                    && dataProp.ValueKind == JsonValueKind.String
                    && dataProp.GetString() is { } data
                    && data.Length > 0)
                {
                    if (data.Length > MaxInputChars)
                    {
                        data = data[..MaxInputChars];
                    }

                    await session.WriteInputAsync(data, cancellationToken);
                }

                break;

            case "resize":
                var cols = doc.RootElement.TryGetProperty("cols", out var colsProp) && colsProp.ValueKind == JsonValueKind.Number
                    ? colsProp.GetInt32()
                    : 0;
                var rows = doc.RootElement.TryGetProperty("rows", out var rowsProp) && rowsProp.ValueKind == JsonValueKind.Number
                    ? rowsProp.GetInt32()
                    : 0;
                if (cols > 0 && rows > 0)
                {
                    session.Resize(cols, rows);
                }

                break;

            default:
                break;
        }
    }

    private static async Task SendLoopAsync(
        WebSocket ws,
        ITerminalPtyAttachment attachment,
        ITerminalPtySession session,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var chunk = await attachment.ReadOutputAsync(cancellationToken);
                if (chunk is null)
                {
                    break;
                }

                await SendFrameAsync(ws, new { type = "output", data = chunk }, CancellationToken.None);
            }

            if (session.EndReason is { } reason)
            {
                await SendFrameAsync(ws, new { type = "error", message = reason }, CancellationToken.None);
            }

            await SendFrameAsync(ws, new { type = "exit", code = session.ExitCode ?? -1 }, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Client went away.
        }
        catch (WebSocketException)
        {
            // Client went away.
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Terminal PTY send loop ended unexpectedly.");
        }
    }

    private static Task SendFrameAsync(WebSocket ws, object frame, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(frame);
        return ws.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private static int ParseSize(string? raw, int fallback, int min, int max)
    {
        if (int.TryParse(raw, out var value))
        {
            return Math.Clamp(value, min, max);
        }

        return fallback;
    }
}
