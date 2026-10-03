namespace MyHomeLab.Application.Dtos;

public record HubInterfaceDto(
    string Name,
    string Ipv4,
    string Subnet,
    string? MacAddress,
    string Kind,
    string? GatewayIp);

public record HubNodeDto(
    string HostName,
    IReadOnlyList<HubInterfaceDto> Interfaces);

public record GatewayNodeDto(
    string IpAddress,
    string? MacAddress,
    string? Hostname);

public record NetworkTopologyDto(
    GatewayNodeDto? Gateway,
    HubNodeDto Hub,
    IReadOnlyList<MachineSummaryDto> Nodes);

public record DiscoveredDeviceDto(
    string IpAddress,
    string? MacAddress,
    string? Hostname,
    string? DeviceType,
    string? SuggestedIcon,
    DateTime FirstSeenUtc,
    DateTime LastSeenUtc,
    Guid? MatchedMachineId,
    string? MatchedMachineName);

public record DiscoveryResultDto(
    IReadOnlyList<DiscoveredDeviceDto> Devices,
    DateTime ScannedAtUtc,
    IReadOnlyList<string> Subnets);
