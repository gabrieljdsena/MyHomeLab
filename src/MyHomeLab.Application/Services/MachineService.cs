using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Errors;
using MyHomeLab.Application.Mapping;
using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Application.Services;

public sealed class MachineService(IMachineRepository repository)
{
    public async Task<IReadOnlyList<MachineSummaryDto>> GetAllAsync(MachineQuery query, CancellationToken cancellationToken = default)
    {
        var machines = await repository.GetAllAsync(query, cancellationToken);
        return machines
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Name)
            .Select(m => m.ToSummary())
            .ToArray();
    }

    public async Task<MachineDetailDto> GetByIdAsync(MachineId id, CancellationToken cancellationToken = default)
    {
        var machine = await repository.GetByIdAsync(id, cancellationToken);
        if (machine is null)
        {
            throw new MachineNotFoundException($"Machine '{id}' was not found.");
        }

        return machine.ToDetail();
    }

    public async Task<MachineDetailDto> CreateAsync(CreateMachineRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureUniqueNameAsync(request.Name, null, cancellationToken);

        var machine = Machine.Create(
            request.Name, request.Description, request.Hostname, request.Icon, request.SortOrder);

        await repository.AddAsync(machine, cancellationToken);
        return await GetByIdAsync(machine.Id, cancellationToken);
    }

    public async Task<MachineDetailDto> UpdateAsync(MachineId id, UpdateMachineRequest request, CancellationToken cancellationToken = default)
    {
        var machine = await GetMachineAsync(id, cancellationToken);

        await EnsureUniqueNameAsync(request.Name, id, cancellationToken);

        machine.UpdateDetails(
            request.Name, request.Description, request.Hostname, request.Icon, request.SortOrder);

        await repository.UpdateAsync(machine, cancellationToken);
        return machine.ToDetail();
    }

    public async Task<MachineDetailDto> PatchAsync(MachineId id, PatchMachineRequest request, CancellationToken cancellationToken = default)
    {
        var machine = await GetMachineAsync(id, cancellationToken);

        var name = request.Name ?? machine.Name;
        if (!string.Equals(name, machine.Name, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureUniqueNameAsync(name, id, cancellationToken);
        }

        machine.UpdateDetails(
            name,
            request.Description ?? machine.Description,
            request.Hostname ?? machine.Hostname,
            request.Icon ?? machine.Icon,
            request.SortOrder ?? machine.SortOrder);

        if (request.IsEnabled == true) { machine.Enable(); }
        else if (request.IsEnabled == false) { machine.Disable(); }

        await repository.UpdateAsync(machine, cancellationToken);
        return machine.ToDetail();
    }

    public async Task DeleteAsync(MachineId id, CancellationToken cancellationToken = default)
    {
        var machine = await GetMachineAsync(id, cancellationToken);
        await repository.DeleteAsync(machine.Id, cancellationToken);
    }

    public async Task RecordReachabilityAsync(
        MachineId id, MachineReachabilityResult result, CancellationToken cancellationToken = default)
    {
        var machine = await repository.GetByIdAsync(id, cancellationToken);
        if (machine is null)
        {
            return;
        }

        machine.SetReachability(
            result.Reachability, result.LatencyMs, seenAtUtc: null,
            result.IpAddress, result.MacAddress);
        await repository.UpdateReachabilityAsync(machine, cancellationToken);
    }

    private async Task<Machine> GetMachineAsync(MachineId id, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new MachineNotFoundException($"Machine '{id}' was not found.");

    private async Task EnsureUniqueNameAsync(string name, MachineId? excludeId, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByNameAsync(name.Trim(), cancellationToken);
        if (existing is not null && existing.Id != excludeId)
        {
            throw new MachineConflictException($"A machine named '{name}' already exists.");
        }
    }
}
