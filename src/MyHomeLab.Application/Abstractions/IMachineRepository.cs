using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Application.Abstractions;

public record MachineQuery(string? Search = null, bool? EnabledOnly = null);

public interface IMachineRepository
{
    Task<IReadOnlyList<Machine>> GetAllAsync(MachineQuery query, CancellationToken cancellationToken);
    Task<Machine?> GetByIdAsync(MachineId id, CancellationToken cancellationToken);
    Task<Machine?> GetByNameAsync(string name, CancellationToken cancellationToken);
    Task AddAsync(Machine machine, CancellationToken cancellationToken);
    Task UpdateAsync(Machine machine, CancellationToken cancellationToken);
    Task DeleteAsync(MachineId id, CancellationToken cancellationToken);
    Task UpdateReachabilityAsync(Machine machine, CancellationToken cancellationToken);
}
