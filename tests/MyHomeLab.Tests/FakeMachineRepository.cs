using MyHomeLab.Application.Abstractions;
using MyHomeLab.Domain;
using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Tests;

public sealed class FakeMachineRepository : IMachineRepository
{
    private readonly Dictionary<MachineId, Machine> _store = [];

    public Task<IReadOnlyList<Machine>> GetAllAsync(MachineQuery query, CancellationToken cancellationToken = default)
    {
        var machines = _store.Values.Where(m =>
            (string.IsNullOrEmpty(query.Search) ||
             m.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
             m.Hostname.Contains(query.Search, StringComparison.OrdinalIgnoreCase)) &&
            (query.EnabledOnly is null || m.IsEnabled == query.EnabledOnly)).ToArray();

        return Task.FromResult<IReadOnlyList<Machine>>(machines);
    }

    public Task<Machine?> GetByIdAsync(MachineId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.GetValueOrDefault(id));

    public Task<Machine?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var machine = _store.Values.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(machine);
    }

    public Task AddAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        if (_store.Values.Any(m => m.Name.Equals(machine.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("duplicate");
        }

        _store[machine.Id] = machine;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        _store[machine.Id] = machine;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(MachineId id, CancellationToken cancellationToken = default)
    {
        _store.Remove(id);
        return Task.CompletedTask;
    }

    public Task UpdateReachabilityAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        _store[machine.Id] = machine;
        return Task.CompletedTask;
    }
}
