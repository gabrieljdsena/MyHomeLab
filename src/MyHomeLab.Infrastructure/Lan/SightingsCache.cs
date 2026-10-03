using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Infrastructure.Lan;

/// In-memory sightings keyed by IP. First-seen sticks while a device keeps showing up;
/// anything unseen for longer than the expiry window drops off as transient.
internal sealed class SightingsCache(Func<DateTime> clock, TimeSpan expiry)
{
    private readonly Dictionary<string, DiscoveredDeviceDto> _sightings = new(StringComparer.OrdinalIgnoreCase);

    public void Merge(IReadOnlyList<(string Ip, string? Mac, string? Hostname, string? DeviceType, string? SuggestedIcon, Guid? MatchedId, string? MatchedName)> found)
    {
        var now = clock();
        foreach (var entry in found)
        {
            if (_sightings.TryGetValue(entry.Ip, out var existing))
            {
                _sightings[entry.Ip] = existing with
                {
                    MacAddress = entry.Mac ?? existing.MacAddress,
                    Hostname = entry.Hostname ?? existing.Hostname,
                    DeviceType = entry.DeviceType ?? existing.DeviceType,
                    SuggestedIcon = entry.SuggestedIcon ?? existing.SuggestedIcon,
                    LastSeenUtc = now,
                    MatchedMachineId = entry.MatchedId,
                    MatchedMachineName = entry.MatchedName,
                };
            }
            else
            {
                _sightings[entry.Ip] = new DiscoveredDeviceDto(
                    entry.Ip, entry.Mac, entry.Hostname, entry.DeviceType, entry.SuggestedIcon,
                    now, now, entry.MatchedId, entry.MatchedName);
            }
        }
    }

    public IReadOnlyList<DiscoveredDeviceDto> PruneAndSnapshot()
    {
        var now = clock();
        var stale = _sightings
            .Where(pair => now - pair.Value.LastSeenUtc > expiry)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var ip in stale)
        {
            _sightings.Remove(ip);
        }

        return _sightings.Values
            .OrderBy(device => device.IpAddress, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
