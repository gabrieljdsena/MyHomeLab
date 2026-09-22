using MyHomeLab.Application.Dtos;
using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Application.Mapping;

public static class AppMapping
{
    public static AppSummaryDto ToSummary(this App app) =>
        new(
            app.Id.Value,
            app.Name,
            app.Description,
            app.Url,
            app.Icon,
            app.Category,
            app.Port,
            app.Tags,
            app.DockerContainer,
            app.HealthStatus.ToString().ToLowerInvariant(),
            app.LastLatencyMs,
            app.IsEnabled,
            app.SortOrder);

    public static AppDetailDto ToDetail(this App app) =>
        new(
            app.Id.Value,
            app.Name,
            app.Description,
            app.Url,
            app.Icon,
            app.Category,
            app.Port,
            app.Tags,
            app.DockerContainer,
            app.HealthCheckEnabled,
            app.HealthCheckIntervalMs,
            app.HealthStatus.ToString().ToLowerInvariant(),
            app.LastHealthCheckUtc,
            app.LastLatencyMs,
            app.IsEnabled,
            app.SortOrder,
            app.CreatedAtUtc,
            app.UpdatedAtUtc);
}
