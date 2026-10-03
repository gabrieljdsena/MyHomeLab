using System.Net;
using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;
using MyHomeLab.Infrastructure.Lan;
using Xunit;

namespace MyHomeLab.Tests;

public class LanDiscoveryTests
{
    [Fact]
    public void Subnet_Computes_Network_Broadcast_And_Cidr()
    {
        var subnet = new LanSubnet(
            "Wi-Fi",
            IPAddress.Parse("192.168.15.22"),
            IPAddress.Parse("255.255.255.0"),
            null);

        Assert.Equal("192.168.15.0", subnet.NetworkAddress.ToString());
        Assert.Equal("192.168.15.255", subnet.BroadcastAddress.ToString());
        Assert.Equal("192.168.15.0/24", subnet.Cidr);
    }

    [Fact]
    public void Subnet_Enumerates_Usable_Hosts_Only()
    {
        var subnet = new LanSubnet(
            "Wi-Fi",
            IPAddress.Parse("192.168.15.22"),
            IPAddress.Parse("255.255.255.0"),
            null);

        var hosts = subnet.EnumerateHosts(1024).ToArray();

        Assert.Equal(254, hosts.Length);
        Assert.Equal("192.168.15.1", hosts[0].ToString());
        Assert.Equal("192.168.15.254", hosts[^1].ToString());
    }

    [Fact]
    public void Subnet_Caps_Enumeration_For_Large_Ranges()
    {
        var subnet = new LanSubnet(
            "Ethernet",
            IPAddress.Parse("10.0.5.5"),
            IPAddress.Parse("255.255.0.0"),
            null);

        Assert.Equal(100, subnet.EnumerateHosts(100).Count());
    }

    [Fact]
    public void Subnet_Contains_Matches_Network_Membership()
    {
        var subnet = new LanSubnet(
            "Wi-Fi",
            IPAddress.Parse("192.168.15.22"),
            IPAddress.Parse("255.255.255.0"),
            null);

        Assert.True(subnet.Contains(IPAddress.Parse("192.168.15.9")));
        Assert.False(subnet.Contains(IPAddress.Parse("192.168.16.9")));
    }

    [Fact]
    public void Sightings_Keep_FirstSeen_And_Refresh_LastSeen()
    {
        var now = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        var clock = () => now;
        var cache = new SightingsCache(clock, TimeSpan.FromMinutes(15));

        cache.Merge([("192.168.15.9", "aa:bb:cc:dd:ee:ff", null, null, null, null, null)]);
        now = now.AddMinutes(5);
        cache.Merge([("192.168.15.9", "aa:bb:cc:dd:ee:ff", "phone.lan", null, null, null, null)]);

        var snapshot = cache.PruneAndSnapshot();
        var device = Assert.Single(snapshot);
        Assert.Equal(new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc), device.FirstSeenUtc);
        Assert.Equal(new DateTime(2026, 10, 3, 12, 5, 0, DateTimeKind.Utc), device.LastSeenUtc);
        Assert.Equal("phone.lan", device.Hostname);
    }

    [Fact]
    public void Sightings_Expire_Transient_Devices()
    {
        var now = DateTime.UtcNow;
        var clock = () => now;
        var cache = new SightingsCache(clock, TimeSpan.FromMinutes(15));

        cache.Merge([("192.168.15.9", "aa:bb:cc:dd:ee:ff", null, null, null, null, null)]);
        now = now.AddMinutes(16);

        Assert.Empty(cache.PruneAndSnapshot());
    }

    [Fact]
    public void Matcher_Matches_By_Ip()
    {
        var machines = new[] { WithAddress("192.168.15.9", "aa:bb:cc:dd:ee:01") };

        var (id, name) = DiscoveredDeviceMatcher.Match("192.168.15.9", "ff:ff:ff:ff:ff:ff", machines);

        Assert.NotNull(id);
        Assert.Equal("Gaming PC", name);
    }

    [Fact]
    public void Matcher_Matches_By_Mac_Case_Insensitively()
    {
        var machines = new[] { WithAddress("192.168.15.9", "aa:bb:cc:dd:ee:01") };

        var (id, name) = DiscoveredDeviceMatcher.Match("192.168.15.99", "AA:BB:CC:DD:EE:01", machines);

        Assert.NotNull(id);
        Assert.Equal("Gaming PC", name);
    }

    [Fact]
    public void Matcher_Returns_Null_For_Unknown_Device()
    {
        var machines = new[] { WithAddress("192.168.15.9", "aa:bb:cc:dd:ee:01") };

        var (id, name) = DiscoveredDeviceMatcher.Match("192.168.15.50", "11:22:33:44:55:66", machines);

        Assert.Null(id);
        Assert.Null(name);
    }

    [Fact]
    public void GatewayIps_Dedupe_Across_Subnets()
    {
        var subnets = new[]
        {
            new LanSubnet("Wi-Fi", IPAddress.Parse("192.168.15.22"), IPAddress.Parse("255.255.255.0"), null, "wireless", ["192.168.15.1"]),
            new LanSubnet("Ethernet", IPAddress.Parse("192.168.15.23"), IPAddress.Parse("255.255.255.0"), null, "wired", ["192.168.15.1"]),
        };

        Assert.Equal(["192.168.15.1"], GatewayResolver.PickGatewayIps(subnets));
    }

    [Fact]
    public void Gateway_Resolves_Mac_And_Hostname()
    {
        var neighbours = new List<(string Ip, string Mac)> { ("192.168.15.1", "aa:bb:cc:dd:ee:ff") };
        var hostnames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["192.168.15.1"] = "router.lan",
        };

        var (mac, hostname) = GatewayResolver.Resolve("192.168.15.1", neighbours, hostnames);

        Assert.Equal("aa:bb:cc:dd:ee:ff", mac);
        Assert.Equal("router.lan", hostname);
    }

    [Fact]
    public void Oui_Looks_Up_Vendor_Ignoring_Separators_And_Case()
    {
        Assert.Equal("Espressif (ESP32/ESP8266)", OuiHints.TryGetVendor("24:d7:eb:11:22:33"));
        Assert.Equal("Raspberry Pi", OuiHints.TryGetVendor("DC-A6-32-11-22-33"));
        Assert.Null(OuiHints.TryGetVendor("00:11:22:33:44:55"));
        Assert.Null(OuiHints.TryGetVendor("not-a-mac"));
        Assert.Null(OuiHints.TryGetVendor(null));
    }

    [Fact]
    public void Guesser_Prefers_Ssdp_Over_Hostname()
    {
        var (deviceType, icon) = DeviceTypeGuesser.Guess("Living Room", "BRAVIA 4K", null, "android-phone");

        Assert.Equal("BRAVIA 4K", deviceType);
        Assert.Equal("tv", icon);
    }

    [Fact]
    public void Guesser_Types_Phone_From_Hostname()
    {
        var (deviceType, icon) = DeviceTypeGuesser.Guess(null, null, null, "android-a1b2c3");

        Assert.Equal("android-a1b2c3", deviceType);
        Assert.Equal("smartphone", icon);
    }

    [Fact]
    public void Guesser_Yields_Vendor_Name_Only_For_Ambiguous_Oui()
    {
        var (deviceType, icon) = DeviceTypeGuesser.Guess(null, null, "Espressif (ESP32/ESP8266)", null);

        Assert.Equal("Espressif (ESP32/ESP8266)", deviceType);
        Assert.Null(icon);
    }

    [Fact]
    public void Guesser_Returns_Nulls_Without_Evidence()
    {
        Assert.Equal((null, null), DeviceTypeGuesser.Guess(null, null, null, null));
    }

    private static Machine WithAddress(string ip, string mac)
    {
        var machine = Machine.Create("Gaming PC", "d", "gaming-pc", "computer", 0);
        machine.SetReachability(MachineReachability.Online, 1, DateTime.UtcNow, ip, mac);
        return machine;
    }
}
