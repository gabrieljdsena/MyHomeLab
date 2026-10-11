using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Application.Abstractions;

public interface ILanDiscoveryService
{
    HubNodeDto GetHub();

    GatewayNodeDto? GetGateway();

    DiscoveryResultDto GetLatest();

    /// Background-friendly scan: ping sweep + ARP table + reverse DNS.
    Task<DiscoveryResultDto> ScanAsync(CancellationToken cancellationToken);

    /// Full manual scan: everything ScanAsync does plus SSDP device identification.
    Task<DiscoveryResultDto> ScanFullAsync(CancellationToken cancellationToken);
}
