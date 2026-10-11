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
    [ProducesResponseType(typeof(PagedLogDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? search,
        [FromQuery] string? application,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var logs = await logService.GetPagedAsync(
            new LogQuery(search, application, page, pageSize),
            cancellationToken);
        return Ok(logs);
    }

    [HttpGet("applications")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogApplications(CancellationToken cancellationToken)
    {
        var applications = await logService.GetApplicationsAsync(cancellationToken);
        return Ok(applications);
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
