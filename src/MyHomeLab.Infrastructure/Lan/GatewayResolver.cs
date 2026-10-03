namespace MyHomeLab.Infrastructure.Lan;

/// Picks the router address(es) from the NIC gateway hints and resolves each to a
/// MAC/hostname using the same ARP + reverse-DNS data as the device scan, so the
/// map can draw the gateway without any extra traffic.
internal static class GatewayResolver
{
    public static IReadOnlyList<string> PickGatewayIps(IReadOnlyList<LanSubnet> subnets)
    {
        return subnets
            .SelectMany(subnet => subnet.GatewayIps ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static (string? Mac, string? Hostname) Resolve(
        string gatewayIp,
        IReadOnlyList<(string Ip, string Mac)> neighbours,
        IReadOnlyDictionary<string, string> hostnames)
    {
        var mac = neighbours
            .FirstOrDefault(entry => string.Equals(entry.Ip, gatewayIp, StringComparison.OrdinalIgnoreCase))
            .Mac;
        hostnames.TryGetValue(gatewayIp, out var hostname);
        return (mac, hostname);
    }
}
