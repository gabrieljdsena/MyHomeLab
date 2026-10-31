using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyHomeLab.Application.Errors;
using MyHomeLab.Domain;
using MyHomeLab.Infrastructure.FileServer;

namespace MyHomeLab.Tests;

public class FileServerChunkedUploadTests : IDisposable
{
    private const int ChunkBytes = 64 * 1024;

    private readonly string root = Path.Combine(Path.GetTempPath(), "myhomelab-chunks-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
        catch
        {
            // best effort
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task SingleChunk_Roundtrip()
    {
        var service = CreateService(quotaMaxBytes: 1024 * 1024);
        var payload = new byte[100];
        new Random(42).NextBytes(payload);

        var session = service.InitUploadSession(null, "big.bin", payload.Length, 1, false);
        Assert.Equal([ ], session.ReceivedChunks);

        await service.AppendUploadChunkAsync(session.UploadId, 0, new MemoryStream(payload), payload.Length, CancellationToken.None);

        var status = service.GetUploadSession(session.UploadId);
        Assert.Equal([0], status.ReceivedChunks);

        var entry = await service.CompleteUploadSessionAsync(session.UploadId, CancellationToken.None);
        Assert.Equal("big.bin", entry.Name);
        Assert.Equal(payload.Length, entry.SizeBytes);
        Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(root, "big.bin")));
    }

    [Fact]
    public async Task MultiChunk_OutOfOrder_Assembles()
    {
        var service = CreateService(quotaMaxBytes: 1024 * 1024);
        var payload = new byte[ChunkBytes * 2 + 12345];
        new Random(7).NextBytes(payload);

        var session = service.InitUploadSession(null, "multi.bin", payload.Length, 3, false);

        // last chunk first, then out of order — resume must not care about sequence
        await AppendSlice(service, session.UploadId, 2, payload, ChunkBytes * 2, 12345);
        await AppendSlice(service, session.UploadId, 0, payload, 0, ChunkBytes);
        Assert.Equal([0, 2], service.GetUploadSession(session.UploadId).ReceivedChunks);

        await AppendSlice(service, session.UploadId, 1, payload, ChunkBytes, ChunkBytes);

        var entry = await service.CompleteUploadSessionAsync(session.UploadId, CancellationToken.None);
        Assert.Equal(payload.Length, entry.SizeBytes);
        Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(root, "multi.bin")));
    }

    [Fact]
    public async Task Complete_MissingChunk_Throws_And_Keeps_Session()
    {
        var service = CreateService(quotaMaxBytes: 1024 * 1024);
        var session = service.InitUploadSession(null, "partial.bin", ChunkBytes + 10, 2, false);
        await AppendSlice(service, session.UploadId, 0, new byte[ChunkBytes + 10], 0, ChunkBytes);

        await Assert.ThrowsAsync<DomainException>(
            () => service.CompleteUploadSessionAsync(session.UploadId, CancellationToken.None));

        // session survives so the client can resume the missing chunk
        Assert.Equal([0], service.GetUploadSession(session.UploadId).ReceivedChunks);
    }

    [Fact]
    public async Task Append_WrongSize_Throws()
    {
        var service = CreateService(quotaMaxBytes: 1024 * 1024);
        var session = service.InitUploadSession(null, "bad.bin", ChunkBytes + 10, 2, false);

        await Assert.ThrowsAsync<DomainException>(
            () => service.AppendUploadChunkAsync(session.UploadId, 0, new MemoryStream(new byte[10]), 10, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(
            () => service.AppendUploadChunkAsync(session.UploadId, 5, new MemoryStream(new byte[10]), 10, CancellationToken.None));
    }

    [Fact]
    public void Init_WrongChunkCount_Throws()
    {
        var service = CreateService(quotaMaxBytes: 1024 * 1024);
        Assert.Throws<DomainException>(() => service.InitUploadSession(null, "bad.bin", ChunkBytes + 10, 1, false));
        Assert.Throws<DomainException>(() => service.InitUploadSession(null, "bad.bin", 0, 1, false));
    }

    [Fact]
    public async Task Init_Respects_Overwrite_And_Quota()
    {
        var service = CreateService(quotaMaxBytes: 100);
        var payload = new byte[60];
        var session = service.InitUploadSession(null, "a.bin", payload.Length, 1, false);
        await service.AppendUploadChunkAsync(session.UploadId, 0, new MemoryStream(payload), payload.Length, CancellationToken.None);
        await service.CompleteUploadSessionAsync(session.UploadId, CancellationToken.None);

        Assert.Throws<SharedFileConflictException>(() => service.InitUploadSession(null, "a.bin", 10, 1, false));
        Assert.Throws<DomainException>(() => service.InitUploadSession(null, "b.bin", 60, 1, false));

        var overwrite = service.InitUploadSession(null, "a.bin", 10, 1, true);
        await service.AppendUploadChunkAsync(overwrite.UploadId, 0, new MemoryStream(new byte[10]), 10, CancellationToken.None);
        var entry = await service.CompleteUploadSessionAsync(overwrite.UploadId, CancellationToken.None);
        Assert.Equal(10, entry.SizeBytes);
    }

    [Fact]
    public async Task TempSessions_Hidden_From_List_And_Quota()
    {
        var service = CreateService(quotaMaxBytes: 1024 * 1024);
        var payload = new byte[ChunkBytes + 10];
        var session = service.InitUploadSession(null, "hidden.bin", payload.Length, 2, false);
        await AppendSlice(service, session.UploadId, 0, payload, 0, ChunkBytes);

        var listing = service.List(null);
        Assert.DoesNotContain(listing.Entries, e => e.Name == ".upload-tmp");
        Assert.Equal(0, service.GetUsedBytes());
    }

    [Fact]
    public async Task Abort_Removes_Session()
    {
        var service = CreateService(quotaMaxBytes: 1024 * 1024);
        var session = service.InitUploadSession(null, "abort.bin", 100, 1, false);
        service.AbortUploadSession(session.UploadId);
        Assert.Throws<SharedFileNotFoundException>(() => service.GetUploadSession(session.UploadId));

        // unknown ids — including traversal attempts — read as not found
        Assert.Throws<SharedFileNotFoundException>(() => service.GetUploadSession("0".PadLeft(32, '0')));
        Assert.Throws<SharedFileNotFoundException>(() => service.GetUploadSession(".."));
        await Task.CompletedTask;
    }

    private static async Task AppendSlice(
        FileSystemFileService service,
        string uploadId,
        int index,
        byte[] payload,
        int offset,
        int count)
    {
        await service.AppendUploadChunkAsync(
            uploadId, index, new MemoryStream(payload, offset, count), count, CancellationToken.None);
    }

    private FileSystemFileService CreateService(long quotaMaxBytes)
    {
        var options = Options.Create(new FileServerOptions
        {
            Enabled = true,
            RootPath = root,
            QuotaMaxBytes = quotaMaxBytes,
            HideSystemFiles = true,
            UploadChunkBytes = ChunkBytes,
        });
        return new FileSystemFileService(options, NullLogger<FileSystemFileService>.Instance);
    }
}
