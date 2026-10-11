namespace MyHomeLab.Application.Dtos;

public record LogDto(int Id, string Application, string Log);

public record PagedLogDto(
    IReadOnlyList<LogDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
