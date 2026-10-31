using Microsoft.AspNetCore.Mvc;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Services;

namespace MyHomeLab.Api.Controllers;

[ApiController]
[Route("api/api-keys")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ApiKeysController(ApiKeyService apiKeys) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        return Ok(await apiKeys.ListAsync(cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateApiKeyResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        CreateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await apiKeys.CreateAsync(request.Name, request.CanWrite, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiKeyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await apiKeys.SetCanWriteAsync(id, request.CanWrite, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        await apiKeys.RevokeAsync(id, cancellationToken);
        return NoContent();
    }
}
