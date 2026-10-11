using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Infrastructure.Lan;

internal static class DiscoveredDeviceMatcher
{
    public static (Guid? Id, string? Name) Match(string ip, string? mac, IReadOnlyList<Machine> machines)
    {
        foreach (var machine in machines)
        {
            if (!string.IsNullOrWhiteSpace(machine.LastIpAddress)
                && string.Equals(machine.LastIpAddress, ip, StringComparison.OrdinalIgnoreCase))
            {
                return (machine.Id.Value, machine.Name);
            }

            if (!string.IsNullOrWhiteSpace(mac)
                && !string.IsNullOrWhiteSpace(machine.LastMacAddress)
                && string.Equals(machine.LastMacAddress, mac, StringComparison.OrdinalIgnoreCase))
            {
                return (machine.Id.Value, machine.Name);
            }
        }

        return (null, null);
    }
}
