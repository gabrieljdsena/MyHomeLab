namespace MyHomeLab.Application.Dtos;

public record MachineBaseDto(
    Guid Id, string Name, string Description, string Hostname, string Icon);

public record MachineSummaryDto(
    Guid Id, string Name, string Description, string Hostname, string Icon,
    string Reachability, int? LastLatencyMs,
    string? IpAddress, string? MacAddress, bool IsEnabled, int SortOrder)
    : MachineBaseDto(Id, Name, Description, Hostname, Icon);

public record MachineDetailDto(
    Guid Id, string Name, string Description, string Hostname, string Icon,
    string Reachability, DateTime? LastSeenUtc, int? LastLatencyMs,
    string? IpAddress, string? MacAddress,
    bool IsEnabled, int SortOrder, DateTime CreatedAtUtc, DateTime UpdatedAtUtc)
    : MachineBaseDto(Id, Name, Description, Hostname, Icon);

public record CreateMachineRequest(
    string Name, string Description, string Hostname, string Icon, int SortOrder);

public record UpdateMachineRequest(
    string Name, string Description, string Hostname, string Icon, int SortOrder);

public record PatchMachineRequest(
    string? Name, string? Description, string? Hostname, string? Icon,
    bool? IsEnabled, int? SortOrder);
