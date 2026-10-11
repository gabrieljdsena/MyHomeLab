namespace MyHomeLab.Infrastructure.Lan;

public sealed class WindowsLanOptions
{
    public bool Enabled { get; init; } = true;

    public int ProbeIntervalSeconds { get; init; } = 30;

    public int ProbeTimeoutMs { get; init; } = 2000;

    public bool DiscoveryEnabled { get; init; } = true;

    public int DiscoveryIntervalSeconds { get; init; } = 300;

    public int DiscoveryTimeoutMs { get; init; } = 400;

    public int DiscoveryMaxConcurrency { get; init; } = 64;

    public int DiscoveryMaxHostsPerSubnet { get; init; } = 1024;

    public int DiscoveryExpiryMinutes { get; init; } = 15;
}
