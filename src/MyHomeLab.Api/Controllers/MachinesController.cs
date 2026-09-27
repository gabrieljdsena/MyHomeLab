using Microsoft.AspNetCore.Mvc;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Services;
using MyHomeLab.Domain;

namespace MyHomeLab.Api.Controllers;

[ApiController]
[Route("api/machines")]
public sealed class MachinesController(MachineService machineService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(MachineSummaryDto[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMachines(
        [FromQuery] string? search,
        [FromQuery] bool? enabledOnly,
        CancellationToken cancellationToken)
    {
        var machines = await machineService.GetAllAsync(
            new MachineQuery(search, enabledOnly),
            cancellationToken);
        return Ok(machines);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MachineDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMachine(Guid id, CancellationToken cancellationToken)
    {
        var machine = await machineService.GetByIdAsync(MachineId.From(id), cancellationToken);
        return Ok(machine);
    }

    [HttpPost]
    [ProducesResponseType(typeof(MachineDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMachine(CreateMachineRequest request, CancellationToken cancellationToken)
    {
        var machine = await machineService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetMachine), new { id = machine.Id }, machine);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(MachineDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMachine(Guid id, UpdateMachineRequest request, CancellationToken cancellationToken)
    {
        var machine = await machineService.UpdateAsync(MachineId.From(id), request, cancellationToken);
        return Ok(machine);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(MachineDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PatchMachine(Guid id, PatchMachineRequest request, CancellationToken cancellationToken)
    {
        var machine = await machineService.PatchAsync(MachineId.From(id), request, cancellationToken);
        return Ok(machine);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMachine(Guid id, CancellationToken cancellationToken)
    {
        await machineService.DeleteAsync(MachineId.From(id), cancellationToken);
        return NoContent();
    }
}
