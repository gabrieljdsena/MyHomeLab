using System;
using System.Text.RegularExpressions;

namespace MyHomeLab.Domain.Entities;

public class Machine
{
    private const int MaxHostnameLength = 255;

    // The hostname is resolved by the reachability probe and shown on the card, so it is kept to
    // shapes a real host can have: NetBIOS names, FQDNs and IPv4 literals. Backslashes, spaces,
    // underscores and relative segments are rejected rather than normalised away.
    private static readonly Regex HostnamePattern = new(
        @"^[a-z0-9](?:[a-z0-9\-]{0,61}[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9\-]{0,61}[a-z0-9])?)*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Written by the reachability probe from the OS neighbor table, so the only shape we ever
    // see is six lowercase hex octets; anything else means the resolver misbehaved.
    private static readonly Regex MacAddressPattern = new(
        @"^[0-9a-f]{2}(?::[0-9a-f]{2}){5}$",
        RegexOptions.Compiled);

    public MachineId Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Hostname { get; private set; } = string.Empty;
    public string Icon { get; private set; } = "computer";
    public bool IsEnabled { get; private set; } = true;
    public int SortOrder { get; private set; }
    public MachineReachability Reachability { get; private set; } = MachineReachability.Unknown;
    public DateTime? LastSeenUtc { get; private set; }
    public int? LastLatencyMs { get; private set; }
    public string? LastIpAddress { get; private set; }
    public string? LastMacAddress { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Machine()
    {
    }

    public static Machine Create(
        string name, string description, string hostname, string icon, int sortOrder)
    {
        var now = DateTime.UtcNow;
        return new Machine
        {
            Id = MachineId.New(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        }
        .ApplyDetails(name, description, hostname, icon, sortOrder);
    }

    public void UpdateDetails(
        string name, string description, string hostname, string icon, int sortOrder)
    {
        ApplyDetails(name, description, hostname, icon, sortOrder);
    }

    public void Enable()
    {
        if (!IsEnabled)
        {
            IsEnabled = true;
            Touch();
        }
    }

    public void Disable()
    {
        if (IsEnabled)
        {
            IsEnabled = false;
            Touch();
        }
    }

    public void MoveTo(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new DomainException("Sort order cannot be negative.");
        }

        if (sortOrder != SortOrder)
        {
            SortOrder = sortOrder;
            Touch();
        }
    }

    public void SetReachability(
        MachineReachability reachability, int? latencyMs, DateTime? seenAtUtc,
        string? ipAddress = null, string? macAddress = null)
    {
        if (latencyMs is < 0)
        {
            throw new DomainException("Latency cannot be negative.");
        }

        if (reachability == MachineReachability.Online)
        {
            LastLatencyMs = latencyMs;
            LastSeenUtc = seenAtUtc ?? DateTime.UtcNow;
        }
        else
        {
            LastLatencyMs = null;
            LastSeenUtc = null;
        }

        // Only overwrite when the probe actually resolved something. A machine that is offline,
        // or reachable only off-subnet where the neighbor table has no entry, keeps the address it
        // last had — which is the one you want while debugging why it went dark.
        LastIpAddress = NormalizeIpAddress(ipAddress) ?? LastIpAddress;
        LastMacAddress = NormalizeMacAddress(macAddress) ?? LastMacAddress;

        Reachability = reachability;
    }

    internal static Machine Restore(
        MachineId id, string name, string description, string hostname, string icon,
        bool isEnabled, int sortOrder,
        MachineReachability reachability, DateTime? lastSeenUtc, int? lastLatencyMs,
        string? lastIpAddress, string? lastMacAddress,
        DateTime createdAtUtc, DateTime updatedAtUtc)
    {
        return new Machine
        {
            Id = id, Name = name, Description = description, Hostname = hostname, Icon = icon,
            IsEnabled = isEnabled, SortOrder = sortOrder,
            Reachability = reachability, LastSeenUtc = lastSeenUtc, LastLatencyMs = lastLatencyMs,
            LastIpAddress = lastIpAddress, LastMacAddress = lastMacAddress,
            CreatedAtUtc = createdAtUtc, UpdatedAtUtc = updatedAtUtc,
        };
    }

    private Machine ApplyDetails(
        string name, string description, string hostname, string icon, int sortOrder)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new DomainException("Name is required.");
        }

        var trimmedHostname = NormalizeHostname(hostname);

        if (sortOrder < 0)
        {
            throw new DomainException("Sort order cannot be negative.");
        }

        Name = trimmedName;
        Description = description?.Trim() ?? string.Empty;
        Hostname = trimmedHostname;
        Icon = string.IsNullOrWhiteSpace(icon) ? "computer" : icon.Trim();
        SortOrder = sortOrder;

        Touch();
        return this;
    }

    private static string NormalizeHostname(string? hostname)
    {
        var trimmed = hostname?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new DomainException("Hostname is required.");
        }

        if (trimmed.Length > MaxHostnameLength || !HostnamePattern.IsMatch(trimmed))
        {
            throw new DomainException("Hostname must be a NetBIOS name, FQDN or IPv4 address.");
        }

        return trimmed;
    }

    // Only overwrite when the probe actually resolved something, and silently keep the previous
    // value when it hands over something malformed. These are cosmetic hints written by an
    // in-process probe, so a bad one must never cost us the reachability reading it came with —
    // the database CHECK constraints remain the hard backstop on format.
    private static string? NormalizeIpAddress(string? ipAddress)
    {
        var trimmed = ipAddress?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (!System.Net.IPAddress.TryParse(trimmed, out var parsed) ||
            parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return null;
        }

        return parsed.ToString();
    }

    private static string? NormalizeMacAddress(string? macAddress)
    {
        var trimmed = macAddress?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(trimmed) || !MacAddressPattern.IsMatch(trimmed))
        {
            return null;
        }

        return trimmed;
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
