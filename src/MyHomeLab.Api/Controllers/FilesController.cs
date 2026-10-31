using Microsoft.AspNetCore.Mvc;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Errors;

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
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
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

        var pending = Request.Form.Files
            .Where(formFile => !string.IsNullOrWhiteSpace(formFile.FileName))
            .Select(formFile => Path.GetFileName(formFile.FileName))
            .ToArray();
        if (pending.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "No files were uploaded.", Status = StatusCodes.Status400BadRequest });
        }

        // Validate the whole batch before writing anything, so a conflict or a full
        // disk can never leave half the files saved while the UI reports failure.
        if (!overwrite)
        {
            var conflicts = pending.Where(name => files.Exists(path, name)).ToArray();
            if (conflicts.Length > 0)
            {
                throw new SharedFileConflictException(
                    conflicts.Length == 1
                        ? $"File '{conflicts[0]}' already exists."
                        : $"{conflicts.Length} files already exist: {string.Join(", ", conflicts)}.");
            }
        }

        files.EnsureQuotaAvailable(Request.Form.Files.Sum(formFile => formFile.Length));

        var saved = new List<FileEntryDto>(pending.Length);
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

    [HttpPost("uploads/init")]
    [ProducesResponseType(typeof(UploadSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult InitUpload([FromBody] InitUploadRequest request)
    {
        return Ok(files.InitUploadSession(
            request.Directory,
            request.FileName,
            request.TotalSizeBytes,
            request.TotalChunks,
            request.Overwrite));
    }

    [HttpGet("uploads/{uploadId}")]
    [ProducesResponseType(typeof(UploadSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult GetUpload(string uploadId)
    {
        return Ok(files.GetUploadSession(uploadId));
    }

    [HttpPut("uploads/{uploadId}/chunks/{chunkIndex:int}")]
    [DisableRequestSizeLimit]
    [RequestSizeLimit(32 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PutChunk(
        string uploadId,
        int chunkIndex,
        CancellationToken cancellationToken)
    {
        if (Request.ContentLength is null)
        {
            return BadRequest(new ProblemDetails { Title = "Bad Request", Detail = "Chunk requires a Content-Length.", Status = StatusCodes.Status400BadRequest });
        }

        await files.AppendUploadChunkAsync(uploadId, chunkIndex, Request.Body, Request.ContentLength.Value, cancellationToken);
        return NoContent();
    }

    [HttpPost("uploads/{uploadId}/complete")]
    [ProducesResponseType(typeof(FileEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CompleteUpload(string uploadId, CancellationToken cancellationToken)
    {
        return Ok(await files.CompleteUploadSessionAsync(uploadId, cancellationToken));
    }

    [HttpDelete("uploads/{uploadId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult AbortUpload(string uploadId)
    {
        files.AbortUploadSession(uploadId);
        return NoContent();
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
