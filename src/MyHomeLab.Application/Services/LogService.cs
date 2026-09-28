using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Errors;
using MyHomeLab.Application.Mapping;

namespace MyHomeLab.Application.Services;

public sealed class LogService(ILogRepository repository)
{
    private const int MinLimit = 1;
    private const int MaxLimit = 1000;
    private const int DefaultLimit = 200;

    public async Task<IReadOnlyList<LogDto>> GetAllAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        var clamped = query with { Limit = ClampLimit(query.Limit) };
        var entries = await repository.GetAllAsync(clamped, cancellationToken);
        return entries
            .OrderByDescending(e => e.Id)
            .Select(e => e.ToDto())
            .ToArray();
    }

    public async Task<LogDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entry = await repository.GetByIdAsync(id, cancellationToken);
        if (entry is null)
        {
            throw new LogNotFoundException($"Log entry '{id}' was not found.");
        }

        return entry.ToDto();
    }

    private static int ClampLimit(int limit)
    {
        if (limit is < MinLimit or > MaxLimit)
        {
            return DefaultLimit;
        }

        return limit;
    }
}
