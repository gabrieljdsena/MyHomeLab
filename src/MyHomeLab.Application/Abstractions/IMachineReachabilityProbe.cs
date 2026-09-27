using MyHomeLab.Domain;

namespace MyHomeLab.Application.Abstractions;

public record MachineReachabilityResult(
    MachineReachability Reachability, int? LatencyMs, string? IpAddress = null, string? MacAddress = null);

public interface IMachineReachabilityProbe
{
    Task<MachineReachabilityResult> ProbeAsync(string hostname, CancellationToken cancellationToken);
}
