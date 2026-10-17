using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Application.Abstractions;

public interface IDeployService
{
    Task<DeployStatusDto> StartDeployAsync(
        Guid appId,
        string serviceName,
        string buildScriptPath,
        CancellationToken cancellationToken = default);

    Task<DeployStatusDto?> GetDeployStatusAsync(Guid appId, CancellationToken cancellationToken = default);
}
