using Microsoft.AspNetCore.Mvc;
using MyHomeLab.Application.Services;

namespace MyHomeLab.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(AppService appService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await appService.GetCategoriesAsync(cancellationToken);
        return Ok(categories);
    }
}