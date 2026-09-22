using System;
using System.Linq;

namespace MyHomeLab.Domain.Entities;

public class App
{
    private const int MinHealthCheckIntervalMs = 1_000;
    private const int MaxHealthCheckIntervalMs = 3_600_000;

    public AppId Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string Icon { get; private set; } = "web";
    public string Category { get; private set; } = "other";
    public int? Port { get; private set; }
    public string[] Tags { get; private set; } = [];
    public string? DockerContainer { get; private set; }
    public bool HealthCheckEnabled { get; private set; } = true;
    public int HealthCheckIntervalMs { get; private set; } = 30_000;
    public AppHealthStatus HealthStatus { get; private set; } = AppHealthStatus.Unknown;
    public DateTime? LastHealthCheckUtc { get; private set; }
    public int? LastLatencyMs { get; private set; }
    public bool IsEnabled { get; private set; } = true;
    public int SortOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private App()
    {
    }

    public static App Create(
        string name,
        string url,
        string description,
        string icon,
        string category,
        int? port,
        string[]? tags,
        string? dockerContainer,
        bool healthCheckEnabled,
        int healthCheckIntervalMs,
        int sortOrder)
    {
        var now = DateTime.UtcNow;
        return new App
        {
            Id = AppId.New(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        }
        .ApplyDetails(name, url, description, icon, category, port, tags, dockerContainer, healthCheckEnabled, healthCheckIntervalMs, sortOrder);
    }

    public void UpdateDetails(
        string name,
        string url,
        string description,
        string icon,
        string category,
        int? port,
        string[]? tags,
        string? dockerContainer,
        bool healthCheckEnabled,
        int healthCheckIntervalMs,
        int sortOrder)
    {
        ApplyDetails(name, url, description, icon, category, port, tags, dockerContainer, healthCheckEnabled, healthCheckIntervalMs, sortOrder);
    }

    public void Enable()
    {
        if (!IsEnabled)
        {
            IsEnabled = true;
            Touch();
        }
    }

    public void Disable()
    {
        if (IsEnabled)
        {
            IsEnabled = false;
            Touch();
        }
    }

    public void MoveTo(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new DomainException("Sort order cannot be negative.");
        }

        if (sortOrder != SortOrder)
        {
            SortOrder = sortOrder;
            Touch();
        }
    }

    public void SetHealth(AppHealthStatus status, int? latencyMs, DateTime? checkedAtUtc)
    {
        HealthStatus = status;
        LastLatencyMs = latencyMs;
        LastHealthCheckUtc = checkedAtUtc ?? DateTime.UtcNow;
    }

    internal static App Restore(
        AppId id,
        string name,
        string description,
        string url,
        string icon,
        string category,
        int? port,
        string[] tags,
        string? dockerContainer,
        bool healthCheckEnabled,
        int healthCheckIntervalMs,
        AppHealthStatus healthStatus,
        DateTime? lastHealthCheckUtc,
        int? lastLatencyMs,
        bool isEnabled,
        int sortOrder,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        return new App
        {
            Id = id,
            Name = name,
            Description = description,
            Url = url,
            Icon = icon,
            Category = category,
            Port = port,
            Tags = tags,
            DockerContainer = NormalizeDockerContainer(dockerContainer),
            HealthCheckEnabled = healthCheckEnabled,
            HealthCheckIntervalMs = healthCheckIntervalMs,
            HealthStatus = healthStatus,
            LastHealthCheckUtc = lastHealthCheckUtc,
            LastLatencyMs = lastLatencyMs,
            IsEnabled = isEnabled,
            SortOrder = sortOrder,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
        };
    }

    private static string? NormalizeDockerContainer(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 128)
        {
            throw new DomainException("Docker container name is too long.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[a-zA-Z0-9][a-zA-Z0-9_.\-]*$"))
        {
            throw new DomainException("Docker container name is invalid.");
        }

        return trimmed.ToLowerInvariant();
    }

    private App ApplyDetails(
        string name,
        string url,
        string description,
        string icon,
        string category,
        int? port,
        string[]? tags,
        string? dockerContainer,
        bool healthCheckEnabled,
        int healthCheckIntervalMs,
        int sortOrder)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new DomainException("Name is required.");
        }

        var trimmedUrl = url?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedUrl) ||
            !Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var parsedUrl) ||
            (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainException("URL must be an absolute http(s) address.");
        }

        if (port is < 1 or > 65535)
        {
            throw new DomainException("Port must be between 1 and 65535.");
        }

        if (sortOrder < 0)
        {
            throw new DomainException("Sort order cannot be negative.");
        }

        if (healthCheckIntervalMs is < MinHealthCheckIntervalMs or > MaxHealthCheckIntervalMs)
        {
            throw new DomainException($"Health check interval must be between {MinHealthCheckIntervalMs} and {MaxHealthCheckIntervalMs} ms.");
        }

        Name = trimmedName;
        Url = trimmedUrl;
        Description = description?.Trim() ?? string.Empty;
        Icon = string.IsNullOrWhiteSpace(icon) ? "web" : icon.Trim();
        Category = string.IsNullOrWhiteSpace(category) ? "other" : category.Trim().ToLowerInvariant();
        Port = port;
        Tags = tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        DockerContainer = NormalizeDockerContainer(dockerContainer);
        HealthCheckEnabled = healthCheckEnabled;
        HealthCheckIntervalMs = healthCheckIntervalMs;
        SortOrder = sortOrder;

        Touch();
        return this;
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
