using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace MyHomeLab.Api.Controllers;

[ApiController]
[Route("api/healthz")]
public sealed class MetaController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        return Ok(new
        {
            status = "ok",
            service = "myhomelab",
            version,
            timeUtc = DateTime.UtcNow,
        });
    }
}