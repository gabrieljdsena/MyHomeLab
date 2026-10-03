using Microsoft.AspNetCore.Mvc;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Api.Controllers;

[ApiController]
[Route("api/files")]
public sealed class FilesController(IFileServerService files) : ControllerBase
{
    [HttpGet("config")]
    [ProducesResponseType(typeof(FileServerConfigDto), StatusCodes.Status200OK)]
    public IActionResult GetConfig()
    {
        return Ok(files.GetConfig());
    }

    [HttpGet]
    [ProducesResponseType(typeof(FileListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult List([FromQuery] string? path)
    {
        return Ok(files.List(path));
    }

    [HttpGet("download")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult Download([FromQuery] string path)
    {
        var (stream, fileName, contentType) = files.OpenRead(path);
        return File(stream, contentType, fileDownloadName: fileName, enableRangeProcessing: true);
    }

    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    [RequestSizeLimit(long.MaxValue)]
    [ProducesResponseType(typeof(FileEntryDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Upload(
        [FromQuery] string? path,
        [FromQuery] bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        if (Request.Form.Files.Count == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "No files were uploaded.", Status = StatusCodes.Status400BadRequest });
        }

        var saved = new List<FileEntryDto>(Request.Form.Files.Count);
        foreach (var formFile in Request.Form.Files)
        {
            if (string.IsNullOrWhiteSpace(formFile.FileName))
            {
                continue;
            }

            await using var stream = formFile.OpenReadStream();
            saved.Add(await files.SaveAsync(path, Path.GetFileName(formFile.FileName), stream, formFile.Length, overwrite, cancellationToken));
        }

        return Ok(saved);
    }

    [HttpPost("mkdir")]
    [ProducesResponseType(typeof(FileEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult Mkdir([FromBody] CreateDirectoryRequest request)
    {
        return Ok(files.CreateDirectory(request.ParentPath, request.Name));
    }

    [HttpPost("rename")]
    [ProducesResponseType(typeof(FileEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult Rename([FromBody] RenameRequest request)
    {
        return Ok(files.Move(request.From, request.To));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult Delete([FromQuery] string path, [FromQuery] bool recursive = false)
    {
        files.Delete(path, recursive);
        return NoContent();
    }
}
