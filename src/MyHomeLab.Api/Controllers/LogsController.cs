using Microsoft.AspNetCore.Mvc;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Services;

namespace MyHomeLab.Api.Controllers;

[ApiController]
[Route("api/logs")]
public sealed class LogsController(LogService logService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(LogDto[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? search,
        [FromQuery] string? application,
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var logs = await logService.GetAllAsync(
            new LogQuery(search, application, limit),
            cancellationToken);
        return Ok(logs);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLog(int id, CancellationToken cancellationToken)
    {
        var log = await logService.GetByIdAsync(id, cancellationToken);
        return Ok(log);
    }
}
