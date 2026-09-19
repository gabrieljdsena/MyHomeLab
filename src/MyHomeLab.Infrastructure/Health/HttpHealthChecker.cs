using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Domain;

namespace MyHomeLab.Infrastructure.Health;

public sealed class HttpHealthChecker(
    IHttpClientFactory httpClientFactory,
    IOptions<HealthOptions> options,
    ILogger<HttpHealthChecker> logger) : IHealthChecker
{
    public async Task<HealthCheckOutcome> ProbeAsync(string url, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("health");
        client.Timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            stopwatch.Stop();
            return new HealthCheckOutcome(AppHealthStatus.Up, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("Health probe to {Url} timed out.", url);
            return new HealthCheckOutcome(AppHealthStatus.Down, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or UriFormatException)
        {
            logger.LogDebug(ex, "Health probe to {Url} failed.", url);
            return new HealthCheckOutcome(AppHealthStatus.Down, null);
        }
    }
}