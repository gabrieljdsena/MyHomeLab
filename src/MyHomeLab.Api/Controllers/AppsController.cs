using Microsoft.AspNetCore.Mvc;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Services;
using MyHomeLab.Domain;

namespace MyHomeLab.Api.Controllers;

[ApiController]
[Route("api/apps")]
public sealed class AppsController(AppService appService, IHealthChecker healthChecker) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AppDetailDto[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApps(
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] bool? enabledOnly,
        CancellationToken cancellationToken)
    {
        var apps = await appService.GetAllAsync(
            new AppQuery(search, category, enabledOnly),
            cancellationToken);
        return Ok(apps);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AppDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApp(Guid id, CancellationToken cancellationToken)
    {
        var app = await appService.GetByIdAsync(AppId.From(id), cancellationToken);
        return Ok(app);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AppDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateApp(CreateAppRequest request, CancellationToken cancellationToken)
    {
        var app = await appService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetApp), new { id = app.Id }, app);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AppDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateApp(Guid id, UpdateAppRequest request, CancellationToken cancellationToken)
    {
        var app = await appService.UpdateAsync(AppId.From(id), request, cancellationToken);
        return Ok(app);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(AppDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PatchApp(Guid id, PatchAppRequest request, CancellationToken cancellationToken)
    {
        var app = await appService.PatchAsync(AppId.From(id), request, cancellationToken);
        return Ok(app);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteApp(Guid id, CancellationToken cancellationToken)
    {
        await appService.DeleteAsync(AppId.From(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/check")]
    [ProducesResponseType(typeof(HealthResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckApp(Guid id, CancellationToken cancellationToken)
    {
        var result = await appService.ProbeAsync(AppId.From(id), healthChecker, cancellationToken);
        return Ok(result);
    }
}