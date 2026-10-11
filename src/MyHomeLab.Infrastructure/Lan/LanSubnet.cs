using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MyHomeLab.Infrastructure.Lan;

public sealed record LanSubnet(
    string InterfaceName,
    IPAddress Address,
    IPAddress Mask,
    string? MacAddress,
    string InterfaceKind = "other",
    IReadOnlyList<string>? GatewayIps = null)
{
    public int PrefixLength => CountPrefixBits(Mask);

    public string Cidr => $"{NetworkAddress}/{PrefixLength}";

    public IPAddress NetworkAddress => ApplyMask(Address, Mask, and: true);

    public IPAddress BroadcastAddress
    {
        get
        {
            var address = Address.GetAddressBytes();
            var mask = Mask.GetAddressBytes();
            var broadcast = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                broadcast[i] = (byte)(address[i] | ~mask[i]);
            }

            return new IPAddress(broadcast);
        }
    }

    public bool Contains(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        return ApplyMask(address, Mask, and: true).Equals(NetworkAddress);
    }

    public IEnumerable<IPAddress> EnumerateHosts(int maxHosts)
    {
        var network = NetworkAddress.GetAddressBytes();
        var broadcast = BroadcastAddress.GetAddressBytes();
        var yielded = 0;

        var current = (uint)(network[0] << 24 | network[1] << 16 | network[2] << 8 | network[3]) + 1;
        var last = (uint)(broadcast[0] << 24 | broadcast[1] << 16 | broadcast[2] << 8 | broadcast[3]);

        while (current < last && yielded < maxHosts)
        {
            yield return new IPAddress([(byte)(current >> 24), (byte)(current >> 16), (byte)(current >> 8), (byte)current]);
            current++;
            yielded++;
        }
    }

    private static IPAddress ApplyMask(IPAddress address, IPAddress mask, bool and)
    {
        var addressBytes = address.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        var result = new byte[4];
        for (var i = 0; i < 4; i++)
        {
            result[i] = and ? (byte)(addressBytes[i] & maskBytes[i]) : (byte)(addressBytes[i] | ~maskBytes[i]);
        }

        return new IPAddress(result);
    }

    private static int CountPrefixBits(IPAddress mask)
    {
        var bits = 0;
        foreach (var octet in mask.GetAddressBytes())
        {
            for (var bit = 7; bit >= 0; bit--)
            {
                if ((octet & (1 << bit)) != 0)
                {
                    bits++;
                }
                else
                {
                    return bits;
                }
            }
        }

        return bits;
    }
}

public static class LanSubnetHelper
{
    public static IReadOnlyList<LanSubnet> GetLocalSubnets()
    {
        var subnets = new List<LanSubnet>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up
                || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback
                || adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            var properties = adapter.GetIPProperties();
            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork
                    || unicast.IPv4Mask is null)
                {
                    continue;
                }

                subnets.Add(new LanSubnet(
                    adapter.Name,
                    unicast.Address,
                    unicast.IPv4Mask,
                    FormatMac(adapter.GetPhysicalAddress()),
                    MapKind(adapter.NetworkInterfaceType),
                    properties.GatewayAddresses
                        .Select(gateway => gateway.Address)
                        .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(address => address.ToString())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray()));
            }
        }

        return subnets;
    }

    private static string MapKind(NetworkInterfaceType type)
    {
        return type switch
        {
            NetworkInterfaceType.Wireless80211 => "wireless",
            NetworkInterfaceType.Ethernet
                or NetworkInterfaceType.FastEthernetT
                or NetworkInterfaceType.FastEthernetFx
                or NetworkInterfaceType.GigabitEthernet => "wired",
            _ => "other",
        };
    }

    private static string? FormatMac(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 6
            ? string.Join(':', bytes.Select(octet => octet.ToString("x2")))
            : null;
    }
}
