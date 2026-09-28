using MyHomeLab.Application.Dtos;
using MyHomeLab.Domain.Entities;

namespace MyHomeLab.Application.Mapping;

public static class LogMapping
{
    public static LogDto ToDto(this LogEntry entry) =>
        new(entry.Id, entry.Application, entry.Log);
}
