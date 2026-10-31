namespace MyHomeLab.Application.Dtos;

public record FileEntryDto(
    string Name,
    string Path,
    bool IsDirectory,
    long SizeBytes,
    DateTime ModifiedAtUtc,
    string? Extension);

public record FileListResponse(
    string Path,
    IReadOnlyList<FileEntryDto> Entries,
    long QuotaUsedBytes,
    long QuotaMaxBytes);

public record FileServerConfigDto(
    bool Enabled,
    string RootName,
    long QuotaMaxBytes);

public record CreateDirectoryRequest(
    string? ParentPath,
    string Name);

public record RenameRequest(
    string From,
    string To);

public record InitUploadRequest(
    string? Directory,
    string FileName,
    long TotalSizeBytes,
    int TotalChunks,
    bool Overwrite);

public record UploadSessionDto(
    string UploadId,
    string Directory,
    string FileName,
    long TotalSizeBytes,
    int TotalChunks,
    int ChunkSizeBytes,
    IReadOnlyList<int> ReceivedChunks);

public record CompleteUploadRequest(
    string UploadId);
