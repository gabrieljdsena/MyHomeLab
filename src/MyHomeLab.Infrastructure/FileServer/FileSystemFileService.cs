using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Errors;
using MyHomeLab.Domain;

namespace MyHomeLab.Infrastructure.FileServer;

public sealed class FileSystemFileService(IOptions<FileServerOptions> options, ILogger<FileSystemFileService> logger) : IFileServerService
{
    private static readonly HashSet<string> SystemFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "desktop.ini",
        "thumbs.db",
        ".ds_store",
    };

    private readonly FileServerOptions config = options.Value;

    public FileServerConfigDto GetConfig()
    {
        var root = FileServerPaths.GetRoot(config.RootPath);
        var rootName = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(rootName))
        {
            rootName = root;
        }

        return new FileServerConfigDto(config.Enabled, rootName, config.QuotaMaxBytes);
    }

    public FileListResponse List(string? relativePath)
    {
        EnsureEnabled();
        var root = EnsureRoot();
        var full = FileServerPaths.Resolve(root, relativePath);

        if (!Directory.Exists(full))
        {
            if (File.Exists(full))
            {
                throw new DomainException("Path is a file, not a directory.");
            }

            throw new SharedFileNotFoundException($"Directory '{relativePath}' was not found.");
        }

        var entries = new List<FileEntryDto>();
        foreach (var dir in SafeEnumerateDirectories(full))
        {
            var name = Path.GetFileName(dir)!;
            if (ShouldHide(name))
            {
                continue;
            }

            entries.Add(new FileEntryDto(
                name,
                FileServerPaths.ToRelative(root, dir),
                true,
                0,
                Directory.GetLastWriteTimeUtc(dir),
                null));
        }

        foreach (var file in SafeEnumerateFiles(full))
        {
            var name = Path.GetFileName(file)!;
            if (ShouldHide(name))
            {
                continue;
            }

            long size;
            DateTime modified;
            try
            {
                var info = new FileInfo(file);
                size = info.Length;
                modified = info.LastWriteTimeUtc;
            }
            catch
            {
                continue;
            }

            entries.Add(new FileEntryDto(
                name,
                FileServerPaths.ToRelative(root, file),
                false,
                size,
                modified,
                Path.GetExtension(name).TrimStart('.').ToLowerInvariant() is { } ext && ext.Length > 0 ? ext : null));
        }

        entries.Sort(static (a, b) =>
        {
            if (a.IsDirectory != b.IsDirectory)
            {
                return a.IsDirectory ? -1 : 1;
            }

            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return new FileListResponse(
            FileServerPaths.ToRelative(root, full),
            entries,
            GetUsedBytes(),
            config.QuotaMaxBytes);
    }

    public (Stream Stream, string FileName, string ContentType) OpenRead(string relativePath)
    {
        EnsureEnabled();
        var root = EnsureRoot();
        var full = FileServerPaths.Resolve(root, relativePath);

        if (!File.Exists(full))
        {
            throw new SharedFileNotFoundException($"File '{relativePath}' was not found.");
        }

        var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        return (stream, Path.GetFileName(full), MapContentType(Path.GetExtension(full)));
    }

    public async Task<FileEntryDto> SaveAsync(
        string? relativeDir,
        string fileName,
        Stream content,
        long? declaredLength,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var root = EnsureRoot();
        FileServerPaths.ValidateSegment(fileName);
        var dirFull = FileServerPaths.Resolve(root, relativeDir);

        if (!Directory.Exists(dirFull))
        {
            throw new SharedFileNotFoundException($"Directory '{relativeDir}' was not found.");
        }

        var destFull = FileServerPaths.Resolve(root, CombineRelative(relativeDir, fileName.Trim()));
        if (Directory.Exists(destFull))
        {
            throw new DomainException("A directory with the same name already exists.");
        }

        if (File.Exists(destFull) && !overwrite)
        {
            throw new SharedFileConflictException($"File '{fileName}' already exists.");
        }

        var max = config.QuotaMaxBytes;
        var usedBefore = GetUsedBytes();
        var existingLength = File.Exists(destFull) ? new FileInfo(destFull).Length : 0;
        var available = max - (usedBefore - existingLength);

        if (declaredLength.HasValue && declaredLength.Value > available)
        {
            throw new DomainException($"Storage quota exceeded (25 GB). Free {FormatBytes(declaredLength.Value - available)} to upload this file.");
        }

        var tempFull = destFull + ".upload-" + Guid.NewGuid().ToString("N");
        long written = 0;
        try
        {
            await using (var dest = new FileStream(tempFull, FileMode.CreateNew, FileAccess.Write, FileShare.None, 80 * 1024, useAsync: true))
            {
                var buffer = new byte[80 * 1024];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    if (usedBefore - existingLength + written + read > max)
                    {
                        throw new DomainException("Storage quota exceeded (25 GB). Upload stopped and removed.");
                    }

                    await dest.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    written += read;
                }
            }

            if (File.Exists(destFull))
            {
                File.Delete(destFull);
            }

            File.Move(tempFull, destFull);

            var info = new FileInfo(destFull);
            logger.LogInformation("File upload dir={Dir} name={Name} bytes={Bytes} overwrite={Overwrite}", relativeDir, fileName, info.Length, overwrite);
            return new FileEntryDto(
                info.Name,
                FileServerPaths.ToRelative(root, destFull),
                false,
                info.Length,
                info.LastWriteTimeUtc,
                Path.GetExtension(info.Name).TrimStart('.').ToLowerInvariant() is { } ext && ext.Length > 0 ? ext : null);
        }
        catch
        {
            try
            {
                if (File.Exists(tempFull))
                {
                    File.Delete(tempFull);
                }
            }
            catch
            {
                // best effort cleanup
            }

            throw;
        }
    }

    public bool Exists(string? relativeDir, string fileName)
    {
        EnsureEnabled();
        var root = EnsureRoot();
        FileServerPaths.ValidateSegment(fileName);
        var dirFull = FileServerPaths.Resolve(root, relativeDir);
        if (!Directory.Exists(dirFull))
        {
            throw new SharedFileNotFoundException($"Directory '{relativeDir}' was not found.");
        }

        var destFull = FileServerPaths.Resolve(root, CombineRelative(relativeDir, fileName.Trim()));
        return File.Exists(destFull) || Directory.Exists(destFull);
    }

    public void EnsureQuotaAvailable(long requiredBytes)
    {
        EnsureEnabled();
        var available = config.QuotaMaxBytes - GetUsedBytes();
        if (requiredBytes > available)
        {
            throw new DomainException($"Storage quota exceeded (25 GB). Free {FormatBytes(requiredBytes - available)} to upload these files.");
        }
    }

    public FileEntryDto CreateDirectory(string? parentPath, string name)
    {
        EnsureEnabled();
        var root = EnsureRoot();
        FileServerPaths.ValidateSegment(name);
        var parentFull = FileServerPaths.Resolve(root, parentPath);

        if (!Directory.Exists(parentFull))
        {
            throw new SharedFileNotFoundException($"Directory '{parentPath}' was not found.");
        }

        var destFull = FileServerPaths.Resolve(root, CombineRelative(parentPath, name.Trim()));
        if (Directory.Exists(destFull) || File.Exists(destFull))
        {
            throw new SharedFileConflictException($"'{name}' already exists.");
        }

        Directory.CreateDirectory(destFull);
        var info = new DirectoryInfo(destFull);
        return new FileEntryDto(info.Name, FileServerPaths.ToRelative(root, destFull), true, 0, info.LastWriteTimeUtc, null);
    }

    public FileEntryDto Move(string from, string to)
    {
        EnsureEnabled();
        var root = EnsureRoot();
        var fromFull = FileServerPaths.Resolve(root, from);
        var toFull = FileServerPaths.Resolve(root, to);

        if (fromFull.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Cannot rename the shared root.");
        }

        var fromIsDir = Directory.Exists(fromFull);
        var fromIsFile = File.Exists(fromFull);
        if (!fromIsDir && !fromIsFile)
        {
            throw new SharedFileNotFoundException($"'{from}' was not found.");
        }

        var toName = Path.GetFileName(toFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        FileServerPaths.ValidateSegment(toName);

        if (Directory.Exists(toFull) || File.Exists(toFull))
        {
            throw new SharedFileConflictException($"'{to}' already exists.");
        }

        var toParent = Path.GetDirectoryName(toFull);
        if (toParent is null || !Directory.Exists(toParent))
        {
            throw new FileNotFoundException("Destination folder does not exist.");
        }

        if (fromIsDir && toFull.StartsWith(fromFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Cannot move a folder into itself.");
        }

        if (fromIsDir)
        {
            Directory.Move(fromFull, toFull);
        }
        else
        {
            File.Move(fromFull, toFull);
        }

        if (Directory.Exists(toFull))
        {
            var dir = new DirectoryInfo(toFull);
            return new FileEntryDto(dir.Name, FileServerPaths.ToRelative(root, toFull), true, 0, dir.LastWriteTimeUtc, null);
        }

        var file = new FileInfo(toFull);
        return new FileEntryDto(
            file.Name,
            FileServerPaths.ToRelative(root, toFull),
            false,
            file.Length,
            file.LastWriteTimeUtc,
            Path.GetExtension(file.Name).TrimStart('.').ToLowerInvariant() is { } ext && ext.Length > 0 ? ext : null);
    }

    public void Delete(string relativePath, bool recursive)
    {
        EnsureEnabled();
        var root = EnsureRoot();
        var full = FileServerPaths.Resolve(root, relativePath);

        if (full.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Cannot delete the shared root.");
        }

        if (Directory.Exists(full))
        {
            if (!recursive && SafeEnumerateEntries(full).Any())
            {
                throw new DomainException("Folder is not empty. Confirm recursive delete.");
            }

            Directory.Delete(full, recursive);
            logger.LogInformation("File delete dir path={Path} recursive={Recursive}", relativePath, recursive);
            return;
        }

        if (File.Exists(full))
        {
            File.Delete(full);
            logger.LogInformation("File delete file path={Path}", relativePath);
            return;
        }

        throw new SharedFileNotFoundException($"'{relativePath}' was not found.");
    }

    public long GetUsedBytes()
    {
        var root = FileServerPaths.GetRoot(config.RootPath);
        if (!Directory.Exists(root))
        {
            return 0;
        }

        var sessionsDir = SessionRoot(root);
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (file.Contains(".upload-", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (file.StartsWith(sessionsDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                    // file vanished mid-enumeration
                }
            }
        }
        catch
        {
            // root unreadable — report what we have
        }

        return total;
    }

    public UploadSessionDto InitUploadSession(
        string? relativeDir,
        string fileName,
        long totalSizeBytes,
        int totalChunks,
        bool overwrite)
    {
        EnsureEnabled();
        var root = EnsureRoot();
        FileServerPaths.ValidateSegment(fileName);
        var dirFull = FileServerPaths.Resolve(root, relativeDir);
        if (!Directory.Exists(dirFull))
        {
            throw new SharedFileNotFoundException($"Directory '{relativeDir}' was not found.");
        }

        var chunkBytes = ChunkSize();
        if (totalSizeBytes < 1)
        {
            throw new DomainException("Upload size must be at least 1 byte.");
        }

        var expectedChunks = (int)((totalSizeBytes + chunkBytes - 1) / chunkBytes);
        if (totalChunks != expectedChunks)
        {
            throw new DomainException($"Chunk count {totalChunks} does not match size {totalSizeBytes} with {chunkBytes}-byte chunks (expected {expectedChunks}).");
        }

        var destFull = FileServerPaths.Resolve(root, CombineRelative(relativeDir, fileName.Trim()));
        if (Directory.Exists(destFull))
        {
            throw new DomainException("A directory with the same name already exists.");
        }

        if (File.Exists(destFull) && !overwrite)
        {
            throw new SharedFileConflictException($"File '{fileName}' already exists.");
        }

        EnsureQuotaForNewFile(destFull, totalSizeBytes);
        SweepStaleSessions(root);

        var uploadId = Guid.NewGuid().ToString("N");
        var sessionDir = SessionDir(root, uploadId);
        Directory.CreateDirectory(sessionDir);
        var meta = new UploadSessionMeta(
            relativeDir is null ? string.Empty : FileServerPaths.ToRelative(root, dirFull),
            fileName.Trim(),
            totalSizeBytes,
            totalChunks,
            chunkBytes,
            overwrite,
            DateTime.UtcNow);
        File.WriteAllText(MetaPath(sessionDir), JsonSerializer.Serialize(meta));

        logger.LogInformation("Upload session init id={UploadId} dir={Dir} name={Name} bytes={Bytes} chunks={Chunks}", uploadId, relativeDir, fileName, totalSizeBytes, totalChunks);
        return ToSessionDto(uploadId, meta, []);
    }

    public UploadSessionDto GetUploadSession(string uploadId)
    {
        EnsureEnabled();
        var root = EnsureRoot();
        var (sessionDir, meta) = LoadSession(root, uploadId);
        return ToSessionDto(uploadId, meta, ReceivedChunkIndexes(sessionDir, meta.TotalChunks));
    }

    public async Task AppendUploadChunkAsync(
        string uploadId,
        int chunkIndex,
        Stream content,
        long chunkLength,
        CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var root = EnsureRoot();
        var (sessionDir, meta) = LoadSession(root, uploadId);

        if (chunkIndex < 0 || chunkIndex >= meta.TotalChunks)
        {
            throw new DomainException($"Chunk index {chunkIndex} is out of range (0..{meta.TotalChunks - 1}).");
        }

        var expected = ExpectedChunkSize(meta, chunkIndex);
        if (chunkLength != expected)
        {
            throw new DomainException($"Chunk {chunkIndex} is {chunkLength} bytes; expected {expected}.");
        }

        var slotPath = ChunkPath(sessionDir, chunkIndex);
        await using (var dest = new FileStream(slotPath, FileMode.Create, FileAccess.Write, FileShare.None, 80 * 1024, useAsync: true))
        {
            var buffer = new byte[80 * 1024];
            long remaining = chunkLength;
            while (remaining > 0)
            {
                var read = await content.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await dest.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                remaining -= read;
            }

            if (remaining != 0)
            {
                throw new DomainException($"Chunk {chunkIndex} ended early ({chunkLength - remaining} of {chunkLength} bytes).");
            }
        }

        if (new FileInfo(slotPath).Length != expected)
        {
            throw new DomainException($"Chunk {chunkIndex} was not stored completely.");
        }
    }

    public async Task<FileEntryDto> CompleteUploadSessionAsync(string uploadId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var root = EnsureRoot();
        var (sessionDir, meta) = LoadSession(root, uploadId);

        var missing = Enumerable.Range(0, meta.TotalChunks)
            .Where(i => !File.Exists(ChunkPath(sessionDir, i)))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new DomainException($"Upload is incomplete; missing chunk(s): {string.Join(", ", missing.Take(10))}.");
        }

        var dirFull = FileServerPaths.Resolve(root, meta.Directory);
        if (!Directory.Exists(dirFull))
        {
            throw new SharedFileNotFoundException($"Directory '{meta.Directory}' was not found.");
        }

        var destFull = FileServerPaths.Resolve(root, CombineRelative(meta.Directory, meta.FileName));
        if (Directory.Exists(destFull))
        {
            throw new DomainException("A directory with the same name already exists.");
        }

        if (File.Exists(destFull) && !meta.Overwrite)
        {
            throw new SharedFileConflictException($"File '{meta.FileName}' already exists.");
        }

        EnsureQuotaForNewFile(destFull, meta.TotalSizeBytes);

        var tempFull = destFull + ".upload-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var dest = new FileStream(tempFull, FileMode.CreateNew, FileAccess.Write, FileShare.None, 80 * 1024, useAsync: true))
            {
                for (var i = 0; i < meta.TotalChunks; i++)
                {
                    await using var slot = new FileStream(ChunkPath(sessionDir, i), FileMode.Open, FileAccess.Read, FileShare.Read, 80 * 1024, useAsync: true);
                    if (slot.Length != ExpectedChunkSize(meta, i))
                    {
                        throw new DomainException($"Chunk {i} is corrupt; re-upload it.");
                    }

                    await slot.CopyToAsync(dest, cancellationToken);
                }
            }

            if (new FileInfo(tempFull).Length != meta.TotalSizeBytes)
            {
                throw new DomainException("Assembled file size does not match the declared size.");
            }

            if (File.Exists(destFull))
            {
                File.Delete(destFull);
            }

            File.Move(tempFull, destFull);
            Directory.Delete(sessionDir, true);

            var info = new FileInfo(destFull);
            logger.LogInformation("Upload session complete id={UploadId} name={Name} bytes={Bytes}", uploadId, meta.FileName, info.Length);
            return new FileEntryDto(
                info.Name,
                FileServerPaths.ToRelative(root, destFull),
                false,
                info.Length,
                info.LastWriteTimeUtc,
                Path.GetExtension(info.Name).TrimStart('.').ToLowerInvariant() is { } ext && ext.Length > 0 ? ext : null);
        }
        catch
        {
            try
            {
                if (File.Exists(tempFull))
                {
                    File.Delete(tempFull);
                }
            }
            catch
            {
                // best effort cleanup
            }

            throw;
        }
    }

    public void AbortUploadSession(string uploadId)
    {
        EnsureEnabled();
        var root = EnsureRoot();
        ValidateUploadId(uploadId);
        var sessionDir = SessionDir(root, uploadId);
        if (!Directory.Exists(sessionDir))
        {
            throw new SharedFileNotFoundException($"Upload '{uploadId}' was not found.");
        }

        Directory.Delete(sessionDir, true);
        logger.LogInformation("Upload session aborted id={UploadId}", uploadId);
    }

    private void EnsureEnabled()
    {
        if (!config.Enabled)
        {
            throw new DomainException("File server is disabled on this host.");
        }
    }

    private string EnsureRoot()
    {
        var root = FileServerPaths.GetRoot(config.RootPath);
        Directory.CreateDirectory(root);
        return root;
    }

    private bool ShouldHide(string name)
    {
        return config.HideSystemFiles && SystemFileNames.Contains(name)
            || name.Equals(config.UploadTempDirName, StringComparison.OrdinalIgnoreCase);
    }

    private int ChunkSize()
    {
        return config.UploadChunkBytes >= 64 * 1024 ? config.UploadChunkBytes : 8 * 1024 * 1024;
    }

    private string SessionRoot(string root)
    {
        return Path.Combine(root, config.UploadTempDirName);
    }

    private string SessionDir(string root, string uploadId)
    {
        ValidateUploadId(uploadId);
        return Path.Combine(SessionRoot(root), uploadId);
    }

    private static string MetaPath(string sessionDir) => Path.Combine(sessionDir, "meta.json");

    private static string ChunkPath(string sessionDir, int index) => Path.Combine(sessionDir, $"chunk-{index:D6}");

    private static void ValidateUploadId(string uploadId)
    {
        if (uploadId.Length != 32 || !uploadId.All(c => char.IsAsciiHexDigit(c)))
        {
            throw new SharedFileNotFoundException($"Upload '{uploadId}' was not found.");
        }
    }

    private (string SessionDir, UploadSessionMeta Meta) LoadSession(string root, string uploadId)
    {
        var sessionDir = SessionDir(root, uploadId);
        var metaPath = MetaPath(sessionDir);
        if (!File.Exists(metaPath))
        {
            throw new SharedFileNotFoundException($"Upload '{uploadId}' was not found.");
        }

        UploadSessionMeta? meta;
        try
        {
            meta = JsonSerializer.Deserialize<UploadSessionMeta>(File.ReadAllText(metaPath));
        }
        catch
        {
            meta = null;
        }

        if (meta is null || meta.TotalChunks < 1 || meta.TotalSizeBytes < 1)
        {
            throw new DomainException("Upload session is corrupt; start a new upload.");
        }

        return (sessionDir, meta);
    }

    private static IReadOnlyList<int> ReceivedChunkIndexes(string sessionDir, int totalChunks)
    {
        var received = new List<int>(totalChunks);
        for (var i = 0; i < totalChunks; i++)
        {
            if (File.Exists(ChunkPath(sessionDir, i)))
            {
                received.Add(i);
            }
        }

        return received;
    }

    private static long ExpectedChunkSize(UploadSessionMeta meta, int index)
    {
        if (index < meta.TotalChunks - 1)
        {
            return meta.ChunkSizeBytes;
        }

        return meta.TotalSizeBytes - ((long)meta.ChunkSizeBytes * (meta.TotalChunks - 1));
    }

    private static UploadSessionDto ToSessionDto(string uploadId, UploadSessionMeta meta, IReadOnlyList<int> received)
    {
        return new UploadSessionDto(uploadId, meta.Directory, meta.FileName, meta.TotalSizeBytes, meta.TotalChunks, meta.ChunkSizeBytes, received);
    }

    private void EnsureQuotaForNewFile(string destFull, long newLength)
    {
        var usedBefore = GetUsedBytes();
        var existingLength = File.Exists(destFull) ? new FileInfo(destFull).Length : 0;
        var available = config.QuotaMaxBytes - (usedBefore - existingLength);
        if (newLength > available)
        {
            throw new DomainException($"Storage quota exceeded (25 GB). Free {FormatBytes(newLength - available)} to upload this file.");
        }
    }

    private void SweepStaleSessions(string root)
    {
        string sessionsRoot = SessionRoot(root);
        if (!Directory.Exists(sessionsRoot))
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddHours(-Math.Max(1, config.UploadSessionTtlHours));
        foreach (var dir in SafeEnumerateDirectories(sessionsRoot))
        {
            try
            {
                if (Directory.GetLastWriteTimeUtc(dir) < cutoff)
                {
                    Directory.Delete(dir, true);
                }
            }
            catch
            {
                // best effort; a live session failing here must not block a new upload
            }
        }
    }

    private sealed record UploadSessionMeta(
        string Directory,
        string FileName,
        long TotalSizeBytes,
        int TotalChunks,
        int ChunkSizeBytes,
        bool Overwrite,
        DateTime CreatedUtc);

    private static string CombineRelative(string? dir, string name)
    {
        var normalized = FileServerPaths.NormalizeRelative(dir);
        return string.IsNullOrEmpty(normalized) ? name : normalized.Replace('/', Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar + name;
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string full)
    {
        try
        {
            return Directory.EnumerateDirectories(full).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string full)
    {
        try
        {
            return Directory.EnumerateFiles(full).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeEnumerateEntries(string full)
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(full).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string MapContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mkv" => "video/x-matroska",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".pdf" => "application/pdf",
            ".txt" or ".log" or ".md" => "text/plain",
            ".json" => "application/json",
            ".apk" => "application/vnd.android.package-archive",
            ".zip" => "application/zip",
            _ => "application/octet-stream",
        };
    }

    private static string FormatBytes(long bytes)
    {
        const long gb = 1024L * 1024 * 1024;
        const long mb = 1024L * 1024;
        if (bytes >= gb)
        {
            return $"{bytes / (double)gb:F1} GB";
        }

        if (bytes >= mb)
        {
            return $"{bytes / (double)mb:F0} MB";
        }

        return $"{bytes / 1024.0:F0} KB";
    }
}
