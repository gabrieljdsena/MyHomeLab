using Microsoft.AspNetCore.Mvc;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Api.Controllers;

[ApiController]
[Route("api/terminal")]
public sealed class TerminalController(ITerminalService terminal, ITerminalPtyService pty) : ControllerBase
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

    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IReadOnlyList<TerminalSessionSummary>), StatusCodes.Status200OK)]
    public IActionResult GetSessions()
    {
        return Ok(pty.ListSessions());
    }

    [HttpDelete("sessions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> KillSession(Guid id, CancellationToken cancellationToken)
    {
        var killed = await pty.KillSessionAsync(id, cancellationToken);
        if (!killed)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Terminal session not found",
                detail: "No live terminal session with that id.");
        }

        return NoContent();
    }
}
