using MyHomeLab.Application.Dtos;
using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Application.Mapping;

public static class MachineMapping
{
    public static MachineSummaryDto ToSummary(this Machine machine) =>
        new(
            machine.Id.Value, machine.Name, machine.Description, machine.Hostname, machine.Icon,
            machine.Reachability.ToString().ToLowerInvariant(),
            machine.LastLatencyMs, machine.LastIpAddress, machine.LastMacAddress,
            machine.IsEnabled, machine.SortOrder);

    public static MachineDetailDto ToDetail(this Machine machine) =>
        new(
            machine.Id.Value, machine.Name, machine.Description, machine.Hostname, machine.Icon,
            machine.Reachability.ToString().ToLowerInvariant(),
            machine.LastSeenUtc, machine.LastLatencyMs,
            machine.LastIpAddress, machine.LastMacAddress,
            machine.IsEnabled, machine.SortOrder,
            machine.CreatedAtUtc, machine.UpdatedAtUtc);
}
