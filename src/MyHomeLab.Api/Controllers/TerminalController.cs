using Microsoft.AspNetCore.Mvc;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Api.Controllers;

[ApiController]
[Route("api/terminal")]
public sealed class TerminalController(ITerminalService terminal) : ControllerBase
{
    [HttpGet("config")]
    [ProducesResponseType(typeof(TerminalConfigDto), StatusCodes.Status200OK)]
    public IActionResult GetConfig()
    {
        return Ok(terminal.GetConfig());
    }

    [HttpPost("exec")]
    [ProducesResponseType(typeof(TerminalExecuteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Execute([FromBody] TerminalExecuteRequest request, CancellationToken cancellationToken)
    {
        var result = await terminal.ExecuteAsync(request, cancellationToken);
        return Ok(result);
    }
}
