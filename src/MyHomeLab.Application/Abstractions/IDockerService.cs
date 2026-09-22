using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Application.Abstractions;

public interface IDockerService
{
    Task<IReadOnlyList<DockerContainerDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<DockerContainerDto?> InspectAsync(string container, CancellationToken cancellationToken = default);

    Task<DockerActionResultDto> ExecuteAsync(string container, string action, CancellationToken cancellationToken = default);
}
