using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Application.Abstractions;

public interface ITerminalService
{
    Task<TerminalExecuteResponse> ExecuteAsync(TerminalExecuteRequest request, CancellationToken cancellationToken);

    TerminalConfigDto GetConfig();
}
