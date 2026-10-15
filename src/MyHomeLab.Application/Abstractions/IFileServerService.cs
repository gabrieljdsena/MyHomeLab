using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Application.Abstractions;

public interface IFileServerService
{
    FileServerConfigDto GetConfig();

    FileListResponse List(string? relativePath);

    (Stream Stream, string FileName, string ContentType) OpenRead(string relativePath);

    Task<FileEntryDto> SaveAsync(
        string? relativeDir,
        string fileName,
        Stream content,
        long? declaredLength,
        bool overwrite,
        CancellationToken cancellationToken);

    bool Exists(string? relativeDir, string fileName);

    void EnsureQuotaAvailable(long requiredBytes);

    FileEntryDto CreateDirectory(string? parentPath, string name);

    FileEntryDto Move(string from, string to);

    void Delete(string relativePath, bool recursive);

    long GetUsedBytes();

    UploadSessionDto InitUploadSession(
        string? relativeDir,
        string fileName,
        long totalSizeBytes,
        int totalChunks,
        bool overwrite);

    UploadSessionDto GetUploadSession(string uploadId);

    Task AppendUploadChunkAsync(
        string uploadId,
        int chunkIndex,
        Stream content,
        long chunkLength,
        CancellationToken cancellationToken);

    Task<FileEntryDto> CompleteUploadSessionAsync(
        string uploadId,
        CancellationToken cancellationToken);

    void AbortUploadSession(string uploadId);
}
