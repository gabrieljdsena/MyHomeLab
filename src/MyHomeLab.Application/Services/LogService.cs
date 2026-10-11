using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Errors;
using MyHomeLab.Application.Mapping;

namespace MyHomeLab.Application.Services;

public sealed class LogService(ILogRepository repository)
{
    private const int MinPage = 1;
    private const int MinPageSize = 1;
    private const int MaxPageSize = 100;
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 50;

    public async Task<PagedLogDto> GetPagedAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < MinPage ? DefaultPage : query.Page;
        var pageSize = query.PageSize is < MinPageSize or > MaxPageSize ? DefaultPageSize : query.PageSize;
        var (entries, totalCount) = await repository.GetPagedAsync(
            query with { Page = page, PageSize = pageSize },
            cancellationToken);
        var items = entries
            .OrderByDescending(e => e.Id)
            .Select(e => e.ToDto())
            .ToArray();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedLogDto(items, page, pageSize, totalCount, totalPages);
    }

    public Task<IReadOnlyList<string>> GetApplicationsAsync(CancellationToken cancellationToken = default) =>
        repository.GetApplicationsAsync(cancellationToken);

    public async Task<LogDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entry = await repository.GetByIdAsync(id, cancellationToken);
        if (entry is null)
        {
            throw new LogNotFoundException($"Log entry '{id}' was not found.");
        }

        return entry.ToDto();
    }
}
