using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;
using Xunit;

namespace MyHomeLab.Tests;

public class MachineTests
{
    private static Machine CreateMachine(string hostname = "GAMING-PC") =>
        Machine.Create(
            "Gaming PC",
            "Living room desktop",
            hostname,
            "computer", sortOrder: 10);

    [Fact]
    public void Create_Assigns_Identity_And_Defaults()
    {
        var machine = CreateMachine();

        Assert.NotEqual(Guid.Empty, machine.Id.Value);
        Assert.Equal("Gaming PC", machine.Name);
        Assert.Equal("computer", machine.Icon);
        Assert.Equal(MachineReachability.Unknown, machine.Reachability);
        Assert.True(machine.IsEnabled);
        Assert.Null(machine.LastSeenUtc);
        Assert.Null(machine.LastLatencyMs);
    }

    [Fact]
    public void Create_Normalizes_Hostname_To_Lowercase()
    {
        var machine = CreateMachine("GAMING-PC.lan.local");

        Assert.Equal("gaming-pc.lan.local", machine.Hostname);
    }

    [Theory]
    [InlineData("PC-01")]
    [InlineData("pc-01.lan.local")]
    [InlineData("192.168.15.22")]
    [InlineData("a")]
    public void Create_Accepts_Valid_Hostnames(string hostname)
    {
        var machine = CreateMachine(hostname);

        Assert.Equal(hostname.ToLowerInvariant(), machine.Hostname);
    }

    [Fact]
    public void Create_Rejects_Empty_Name()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Machine.Create("  ", "d", "PC-01", "computer", 0));

        Assert.Equal("Name is required.", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_Rejects_Empty_Hostname(string? hostname)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Machine.Create("Gaming PC", "d", hostname!, "computer", 0));

        Assert.Equal("Hostname is required.", ex.Message);
    }

    [Theory]
    // The hostname is handed to DNS resolution and shown on the card, so values that are not
    // hostnames at all (path traversal, shell metacharacters, hosts:ports) must be rejected.
    [InlineData(@"PC-01\c$")]
    [InlineData(@"\\evil")]
    [InlineData("../etc")]
    [InlineData("..")]
    [InlineData("PC_01")]
    [InlineData("PC 01")]
    [InlineData("PC-01.")]
    [InlineData(".PC-01")]
    [InlineData("PC-01$(whoami)")]
    [InlineData("PC-01%PATH%")]
    [InlineData("PC-01&calc")]
    [InlineData("PC-01|calc")]
    [InlineData("localhost:8080")]
    public void Create_Rejects_Unsafe_Hostnames(string hostname)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Machine.Create("Gaming PC", "d", hostname, "computer", 0));

        Assert.Equal("Hostname must be a NetBIOS name, FQDN or IPv4 address.", ex.Message);
    }

    [Fact]
    public void Create_Rejects_Negative_Sort_Order()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Machine.Create("Gaming PC", "d", "PC-01", "computer", -1));

        Assert.Equal("Sort order cannot be negative.", ex.Message);
    }

    [Fact]
    public void Enable_And_Disable_Toggle_IsEnabled()
    {
        var machine = CreateMachine();

        machine.Disable();
        Assert.False(machine.IsEnabled);

        machine.Disable();
        Assert.False(machine.IsEnabled);

        machine.Enable();
        Assert.True(machine.IsEnabled);
    }

    [Fact]
    public void SetReachability_Online_Records_Latency_And_Seen()
    {
        var machine = CreateMachine();
        var seen = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        machine.SetReachability(MachineReachability.Online, 42, seen);

        Assert.Equal(MachineReachability.Online, machine.Reachability);
        Assert.Equal(42, machine.LastLatencyMs);
        Assert.Equal(seen, machine.LastSeenUtc);
    }

    [Fact]
    public void SetReachability_Offline_Clears_Latency_And_Seen()
    {
        var machine = CreateMachine();
        machine.SetReachability(MachineReachability.Online, 42, DateTime.UtcNow);

        machine.SetReachability(MachineReachability.Offline, null, null);

        Assert.Equal(MachineReachability.Offline, machine.Reachability);
        Assert.Null(machine.LastLatencyMs);
        Assert.Null(machine.LastSeenUtc);
    }

    [Fact]
    public void SetReachability_Records_Resolved_Address()
    {
        var machine = CreateMachine();

        machine.SetReachability(MachineReachability.Online, 7, null, "192.168.15.42", "A4:BB:6D:11:22:33");

        Assert.Equal("192.168.15.42", machine.LastIpAddress);
        Assert.Equal("a4:bb:6d:11:22:33", machine.LastMacAddress);
    }

    [Fact]
    public void SetReachability_Keeps_Last_Address_When_Probe_Resolves_Nothing()
    {
        var machine = CreateMachine();
        machine.SetReachability(MachineReachability.Online, 7, null, "192.168.15.42", "a4:bb:6d:11:22:33");

        machine.SetReachability(MachineReachability.Offline, null, null);
        Assert.Equal("192.168.15.42", machine.LastIpAddress);
        Assert.Equal("a4:bb:6d:11:22:33", machine.LastMacAddress);

        // An online host reachable off-subnet resolves an IP but has no neighbour-table entry.
        machine.SetReachability(MachineReachability.Online, 7, null, "10.0.0.9", null);
        Assert.Equal("10.0.0.9", machine.LastIpAddress);
        Assert.Equal("a4:bb:6d:11:22:33", machine.LastMacAddress);
    }

    [Fact]
    public void SetReachability_Ignores_Malformed_Address_Instead_Of_Failing_The_Probe()
    {
        var machine = CreateMachine();
        machine.SetReachability(MachineReachability.Online, 7, null, "192.168.15.42", "a4:bb:6d:11:22:33");

        // A cosmetic hint from the probe must never cost us the reachability reading it arrived with.
        machine.SetReachability(
            MachineReachability.Online, 7, null, "not-an-ip", "a4-bb-6d-11-22-33");

        Assert.Equal(MachineReachability.Online, machine.Reachability);
        Assert.Equal(7, machine.LastLatencyMs);
        Assert.Equal("192.168.15.42", machine.LastIpAddress);
        Assert.Equal("a4:bb:6d:11:22:33", machine.LastMacAddress);

        machine.SetReachability(MachineReachability.Online, 7, null, "fe80::1", null);
        Assert.Equal("192.168.15.42", machine.LastIpAddress);
        Assert.Equal("a4:bb:6d:11:22:33", machine.LastMacAddress);
    }

    [Fact]
    public void SetReachability_Rejects_Negative_Latency()
    {
        var machine = CreateMachine();

        var ex = Assert.Throws<DomainException>(() =>
            machine.SetReachability(MachineReachability.Online, -1, DateTime.UtcNow));

        Assert.Equal("Latency cannot be negative.", ex.Message);
    }

    [Fact]
    public void SetReachability_Does_Not_Bump_UpdatedAt()
    {
        var machine = CreateMachine();
        var before = machine.UpdatedAtUtc;

        machine.SetReachability(MachineReachability.Online, 5, DateTime.UtcNow);

        Assert.Equal(before, machine.UpdatedAtUtc);
    }

    [Fact]
    public void MoveTo_Rejects_Negative_Sort_Order()
    {
        var machine = CreateMachine();

        var ex = Assert.Throws<DomainException>(() => machine.MoveTo(-1));

        Assert.Equal("Sort order cannot be negative.", ex.Message);
    }

    [Fact]
    public void UpdateDetails_Changes_Fields_And_Touches()
    {
        var machine = CreateMachine();
        var before = machine.UpdatedAtUtc;

        machine.UpdateDetails("Office PC", "Renamed", "office-pc", "desktop_mac", 1);

        Assert.Equal("Office PC", machine.Name);
        Assert.Equal("office-pc", machine.Hostname);
        Assert.Equal("desktop_mac", machine.Icon);
        Assert.Equal(1, machine.SortOrder);
        Assert.True(machine.UpdatedAtUtc >= before);
    }

    [Fact]
    public void UpdateDetails_Falls_Back_To_Default_Icon_When_Blank()
    {
        var machine = CreateMachine();

        machine.UpdateDetails("Gaming PC", "d", "PC-01", "   ", 0);

        Assert.Equal("computer", machine.Icon);
    }
}
