using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace MyHomeLab.Infrastructure.Lan;

/// Resolves the hardware address of an on-link IPv4 neighbour via the IP helper API.
/// Returns null whenever the address is off-subnet, unresolved or the platform is not Windows,
/// so callers can treat the MAC as optional metadata rather than a requirement.
internal static class ArpMacResolver
{
    private const uint IpSuccess = 0;
    private const int MacLength = 6;

    public static string? Resolve(IPAddress address)
    {
        if (!OperatingSystem.IsWindows() || address.AddressFamily != AddressFamily.InterNetwork)
        {
            return null;
        }

        var destination = ToNativeUInt32(address);
        var buffer = new byte[MacLength];
        var length = (uint)MacLength;

        // srcIp 0 lets the stack pick the source address for the probe.
        if (SendARP(destination, 0, buffer, ref length) != IpSuccess || length != MacLength)
        {
            return null;
        }

        return string.Join(':', buffer.Select(octet => octet.ToString("x2")));
    }

    // SendARP takes the address as a host-order DWORD holding the four octets in network order.
    private static uint ToNativeUInt32(IPAddress address)
    {
        var octets = address.GetAddressBytes();
        return (uint)(octets[0] | (octets[1] << 8) | (octets[2] << 16) | (octets[3] << 24));
    }

    // DllImport rather than LibraryImport: the source generator requires AllowUnsafeBlocks for the
    // whole Infrastructure assembly, which is a larger relaxation than this one call justifies.
    // ExactSpelling is required because the export is `SendARP`; without it the CLR appends `A`/`W`.
#pragma warning disable SYSLIB1054
    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern uint SendARP(uint destination, uint source, byte[] macAddress, ref uint macAddressLength);
#pragma warning restore SYSLIB1054
}
