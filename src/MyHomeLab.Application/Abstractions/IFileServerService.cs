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

    FileEntryDto CreateDirectory(string? parentPath, string name);

    FileEntryDto Move(string from, string to);

    void Delete(string relativePath, bool recursive);

    long GetUsedBytes();
}
