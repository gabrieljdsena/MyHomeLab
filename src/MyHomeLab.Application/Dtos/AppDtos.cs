namespace MyHomeLab.Application.Dtos;

public record AppBaseDto(
    Guid Id,
    string Name,
    string Description,
    string Url,
    string Icon,
    string Category,
    int? Port,
    string[] Tags,
    string? DockerContainer,
    string? ServiceName,
    string? BuildScriptPath);

public record AppSummaryDto(
    Guid Id,
    string Name,
    string Description,
    string Url,
    string Icon,
    string Category,
    int? Port,
    string[] Tags,
    string? DockerContainer,
    string? ServiceName,
    string? BuildScriptPath,
    string HealthStatus,
    int? LastLatencyMs,
    bool IsEnabled,
    int SortOrder) : AppBaseDto(Id, Name, Description, Url, Icon, Category, Port, Tags, DockerContainer, ServiceName, BuildScriptPath);

public record AppDetailDto(
    Guid Id,
    string Name,
    string Description,
    string Url,
    string Icon,
    string Category,
    int? Port,
    string[] Tags,
    string? DockerContainer,
    string? ServiceName,
    string? BuildScriptPath,
    bool HealthCheckEnabled,
    int HealthCheckIntervalMs,
    string HealthStatus,
    DateTime? LastHealthCheckUtc,
    int? LastLatencyMs,
    bool IsEnabled,
    int SortOrder,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc) : AppBaseDto(Id, Name, Description, Url, Icon, Category, Port, Tags, DockerContainer, ServiceName, BuildScriptPath);

public record CreateAppRequest(
    string Name,
    string Url,
    string Description,
    string Icon,
    string Category,
    int? Port,
    string[] Tags,
    string? DockerContainer,
    string? ServiceName,
    string? BuildScriptPath,
    bool HealthCheckEnabled,
    int HealthCheckIntervalMs,
    int SortOrder);

public record UpdateAppRequest(
    string Name,
    string Url,
    string Description,
    string Icon,
    string Category,
    int? Port,
    string[] Tags,
    string? DockerContainer,
    string? ServiceName,
    string? BuildScriptPath,
    bool HealthCheckEnabled,
    int HealthCheckIntervalMs,
    int SortOrder);

public record PatchAppRequest(
    string? Name,
    string? Url,
    string? Description,
    string? Icon,
    string? Category,
    int? Port,
    string[]? Tags,
    string? DockerContainer,
    string? ServiceName,
    string? BuildScriptPath,
    bool? HealthCheckEnabled,
    bool? IsEnabled,
    int? HealthCheckIntervalMs,
    int? SortOrder);

public record HealthResultDto(string Status, int? LatencyMs);

public record DeployStatusDto(
    Guid AppId,
    string State,
    string Message,
    int? ExitCode,
    string OutputTail,
    DateTime StartedAtUtc,
    DateTime UpdatedAtUtc);
