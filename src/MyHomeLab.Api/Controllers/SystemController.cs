using Microsoft.AspNetCore.Mvc;
using MyHomeLab.Api.Services;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController(
    SystemMetricsService metrics,
    SystemSensorService sensors,
    StorageSmartService smart,
    NetworkThroughputService network,
    PostgresMetricsService postgres,
    IPowerService power) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(SystemMetricsDto), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var snapshot = metrics.GetMetrics();

        // LHM gives CPU/GPU/board temps; SMART gives disk temps. Merge so the Temperatures card
        // is not empty when the CPU probe returns no data (see DELL-SERVER1: LHM returns n/a).
        var temps = sensors.ReadTemperatures()
            .Concat(smart.ReadDiskTemperatures())
            .ToList();

        // Storage health is now primarily driven by Windows Storage reliability counters (NVMe Wear +
        // Temperature). Keep LHM storage health as a fallback if SMART yields nothing (e.g. on a
        // different host where LHM storage doesn't hang).
        var storageHealth = smart.ReadStorageHealth();
        if (storageHealth.Count == 0)
        {
            storageHealth = sensors.ReadStorageHealth();
        }

        return Ok(snapshot with
        {
            Temperatures = temps,
            StorageHealth = storageHealth,
        });
    }

    [HttpGet("network")]
    [ProducesResponseType(typeof(IReadOnlyList<NetworkSampleDto>), StatusCodes.Status200OK)]
    public IActionResult Network()
    {
        return Ok(network.GetSamples());
    }

    [HttpGet("postgres")]
    [ProducesResponseType(typeof(PostgresMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Postgres(CancellationToken cancellationToken)
    {
        return Ok(await postgres.GetMetricsAsync(cancellationToken));
    }

    [HttpPost("power")]
    [ProducesResponseType(typeof(PowerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Power([FromBody] PowerRequest request, CancellationToken cancellationToken)
    {
        var result = await power.ExecuteAsync(request.Action, cancellationToken);
        return Ok(result);
    }
}