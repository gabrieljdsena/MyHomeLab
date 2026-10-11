using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Infrastructure.Lan;

public sealed class LanDiscoveryService(
    IOptions<WindowsLanOptions> options,
    IMachineRepository machines,
    ILogger<LanDiscoveryService> logger) : ILanDiscoveryService
{
    private readonly WindowsLanOptions _config = options.Value;
    private readonly object _gate = new();
    private readonly SightingsCache _sightings =
        new(() => DateTime.UtcNow, TimeSpan.FromMinutes(Math.Max(1, options.Value.DiscoveryExpiryMinutes)));
    private DateTime _lastScanUtc = DateTime.MinValue;
    private IReadOnlyList<string> _lastSubnets = [];
    private GatewayNodeDto? _lastGateway;

    public HubNodeDto GetHub()
    {
        var subnets = LanSubnetHelper.GetLocalSubnets();
        return new HubNodeDto(
            Dns.GetHostName(),
            subnets.Select(subnet => new HubInterfaceDto(
                subnet.InterfaceName,
                subnet.Address.ToString(),
                subnet.Cidr,
                subnet.MacAddress,
                subnet.InterfaceKind,
                subnet.GatewayIps is { Count: > 0 } gateways ? gateways[0] : null)).ToArray());
    }

    public GatewayNodeDto? GetGateway()
    {
        lock (_gate)
        {
            return _lastGateway;
        }
    }

    public DiscoveryResultDto GetLatest()
    {
        lock (_gate)
        {
            return new DiscoveryResultDto(_sightings.PruneAndSnapshot(), _lastScanUtc, _lastSubnets);
        }
    }

    public Task<DiscoveryResultDto> ScanAsync(CancellationToken cancellationToken) =>
        ScanInternalAsync(includeSsdp: false, cancellationToken);

    public Task<DiscoveryResultDto> ScanFullAsync(CancellationToken cancellationToken) =>
        ScanInternalAsync(includeSsdp: true, cancellationToken);

    private async Task<DiscoveryResultDto> ScanInternalAsync(bool includeSsdp, CancellationToken cancellationToken)
    {
        var subnets = LanSubnetHelper.GetLocalSubnets();
        if (subnets.Count == 0)
        {
            logger.LogWarning("LAN discovery found no usable IPv4 interfaces; scan skipped.");
            return GetLatest();
        }

        var ownIps = subnets.Select(subnet => subnet.Address.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var gatewayIps = GatewayResolver.PickGatewayIps(subnets);
        foreach (var subnet in subnets)
        {
            await PingSweepAsync(subnet, cancellationToken);
        }

        var neighbours = ArpTableReader.Read()
            .Where(entry => !ownIps.Contains(entry.Ip) && IsInScannedSubnet(entry.Ip, subnets))
            .Distinct()
            .ToArray();

        var allIps = neighbours
            .Select(entry => entry.Ip)
            .Concat(gatewayIps.Where(ip => !ownIps.Contains(ip)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hostnames = await ResolveHostnamesAsync(allIps, cancellationToken);

        Dictionary<string, (string? Name, string? Model)> ssdp = new(StringComparer.OrdinalIgnoreCase);
        if (includeSsdp)
        {
            ssdp = await SsdpProbe.DiscoverAsync(TimeSpan.FromSeconds(3), cancellationToken);
        }

        var registry = await machines.GetAllAsync(new MachineQuery(), cancellationToken);

        var found = neighbours
            .Where(entry => !gatewayIps.Contains(entry.Ip, StringComparer.OrdinalIgnoreCase))
            .Select(entry =>
            {
                var (id, name) = DiscoveredDeviceMatcher.Match(entry.Ip, entry.Mac, registry);
                hostnames.TryGetValue(entry.Ip, out var hostname);
                ssdp.TryGetValue(entry.Ip, out var description);
                var (deviceType, suggestedIcon) = DeviceTypeGuesser.Guess(
                    description.Name, description.Model, OuiHints.TryGetVendor(entry.Mac), hostname);
                return (entry.Ip, (string?)entry.Mac, Hostname: hostname, DeviceType: deviceType, SuggestedIcon: suggestedIcon, MatchedId: id, MatchedName: name);
            })
            .ToArray();

        var gateway = gatewayIps
            .Select(ip =>
            {
                var (mac, hostname) = GatewayResolver.Resolve(ip, neighbours, hostnames);
                return new GatewayNodeDto(ip, mac, hostname);
            })
            .FirstOrDefault();

        var scannedAt = DateTime.UtcNow;
        lock (_gate)
        {
            _sightings.Merge(found);
            _lastScanUtc = scannedAt;
            _lastSubnets = subnets.Select(subnet => subnet.Cidr).Distinct().ToArray();
            _lastGateway = gateway;
            return new DiscoveryResultDto(_sightings.PruneAndSnapshot(), scannedAt, _lastSubnets);
        }
    }

    private async Task PingSweepAsync(LanSubnet subnet, CancellationToken cancellationToken)
    {
        var targets = subnet.EnumerateHosts(Math.Max(1, _config.DiscoveryMaxHostsPerSubnet)).ToArray();
        using var gate = new SemaphoreSlim(Math.Max(1, _config.DiscoveryMaxConcurrency));
        var timeout = Math.Clamp(_config.DiscoveryTimeoutMs, 100, 5000);

        // A warm ARP cache is the real goal: every echo populates the neighbour table,
        // which ArpTableReader then harvests — including hosts that answer ARP but not ping.
        await Task.WhenAll(targets.Select(async address =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                using var ping = new Ping();
                await ping.SendPingAsync(address, timeout);
            }
            catch
            {
                // unreachable is the common case; the ARP table is the source of truth
            }
            finally
            {
                gate.Release();
            }
        }));

        logger.LogInformation(
            "LAN discovery swept {Hosts} hosts on {Subnet} ({Interface}).",
            targets.Length, subnet.Cidr, subnet.InterfaceName);
    }

    private static bool IsInScannedSubnet(string ip, IReadOnlyList<LanSubnet> subnets)
    {
        return IPAddress.TryParse(ip, out var address)
            && address.AddressFamily == AddressFamily.InterNetwork
            && subnets.Any(subnet => subnet.Contains(address));
    }

    private static async Task<Dictionary<string, string>> ResolveHostnamesAsync(
        string[] ips, CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(32);
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await Task.WhenAll(ips.Select(async ip =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var entry = await Dns.GetHostEntryAsync(ip, cancellationToken)
                    .WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
                if (!string.Equals(entry.HostName, ip, StringComparison.OrdinalIgnoreCase))
                {
                    lock (results)
                    {
                        results[ip] = entry.HostName.TrimEnd('.');
                    }
                }
            }
            catch
            {
                // most DHCP devices have no reverse record; the IP stays the identifier
            }
            finally
            {
                gate.Release();
            }
        }));

        return results;
    }
}
