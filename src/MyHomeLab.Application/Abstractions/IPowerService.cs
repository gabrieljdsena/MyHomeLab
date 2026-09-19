using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Application.Abstractions;

public interface IPowerService
{
    Task<PowerResponse> ExecuteAsync(string action, CancellationToken cancellationToken);
}
