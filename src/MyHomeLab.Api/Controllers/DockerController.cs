using Microsoft.AspNetCore.Mvc;
using MyHomeLab.Application.Abstractions;

namespace MyHomeLab.Api.Controllers;

[ApiController]
[Route("api/docker")]
public sealed class DockerController(IDockerService dockerService) : ControllerBase
{
    [HttpGet("containers")]
    [ProducesResponseType(typeof(Application.Dtos.DockerContainerDto[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListContainers(CancellationToken cancellationToken)
    {
        var containers = await dockerService.ListAsync(cancellationToken);
        return Ok(containers);
    }

    [HttpGet("containers/{name}")]
    [ProducesResponseType(typeof(Application.Dtos.DockerContainerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> InspectContainer(string name, CancellationToken cancellationToken)
    {
        var container = await dockerService.InspectAsync(name, cancellationToken);
        if (container is null)
        {
            return NotFound(new ProblemDetails { Title = "Not Found", Detail = $"Container '{name}' was not found.", Status = StatusCodes.Status404NotFound });
        }

        return Ok(container);
    }

    [HttpPost("containers/{name}/{dockerAction}")]
    [ProducesResponseType(typeof(Application.Dtos.DockerActionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExecuteContainerAction(string name, string dockerAction, CancellationToken cancellationToken)
    {
        var result = await dockerService.ExecuteAsync(name, dockerAction, cancellationToken);
        return Ok(result);
    }
}
