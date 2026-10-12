using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Application.Abstractions;

public interface IToolchainService
{
    string? ResolvePath(string tool);

    Task<IReadOnlyList<ToolInfo>> ProbeAllAsync(CancellationToken cancellationToken);
}
