using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Domain;

namespace MyHomeLab.Infrastructure.Lan;

public sealed class WindowsLanReachabilityProbe(
    IOptions<WindowsLanOptions> options,
    ILogger<WindowsLanReachabilityProbe> logger) : IMachineReachabilityProbe
{
    private WindowsLanOptions Options => options.Value;

    public async Task<MachineReachabilityResult> ProbeAsync(string hostname, CancellationToken cancellationToken)
    {
        var ipAddress = await ResolveIpv4Async(hostname, cancellationToken);

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(hostname, Options.ProbeTimeoutMs);

            return reply.Status switch
            {
                // A successful echo means the neighbour entry already exists, so the MAC lookup
                // below is served from the ARP cache rather than putting another packet on the wire.
                IPStatus.Success => new MachineReachabilityResult(
                    MachineReachability.Online, (int)reply.RoundtripTime, ipAddress, ResolveMac(ipAddress)),
                IPStatus.TimedOut => new MachineReachabilityResult(MachineReachability.Offline, null),
                _ => new MachineReachabilityResult(MachineReachability.Unknown, null),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PingException ex)
        {
            logger.LogDebug(ex, "Ping for {Hostname} failed.", hostname);
            return new MachineReachabilityResult(MachineReachability.Unknown, null);
        }
    }

    // Deliberately ignores PingReply.Address: a dual-stack host usually answers the echo over IPv6,
    // and a link-local v6 address is both useless as a display value and unresolvable via SendARP.
    // The IPv4 sibling is what identifies the machine on the LAN, so resolve that instead.
    private static async Task<string?> ResolveIpv4Async(string hostname, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(hostname, out var literal))
        {
            return literal.AddressFamily == AddressFamily.InterNetwork ? literal.ToString() : null;
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(hostname, cancellationToken);
            return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString();
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static string? ResolveMac(string? ipAddress) =>
        ipAddress is not null && IPAddress.TryParse(ipAddress, out var address)
            ? ArpMacResolver.Resolve(address)
            : null;
}
