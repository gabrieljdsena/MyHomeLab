using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyHomeLab.Application.Errors;
using MyHomeLab.Domain;
using MyHomeLab.Infrastructure.FileServer;

namespace MyHomeLab.Tests;

public class FileServerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "myhomelab-files-" + Guid.NewGuid().ToString("N"));

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
    public void Resolve_Root_For_Empty_Relative()
    {
        Assert.Equal(root, FileServerPaths.Resolve(root, null));
        Assert.Equal(root, FileServerPaths.Resolve(root, ""));
        Assert.Equal(root, FileServerPaths.Resolve(root, "/"));
    }

    [Fact]
    public void Resolve_Allows_Subdirectory()
    {
        var full = FileServerPaths.Resolve(root, "a/b");
        Assert.Equal(Path.Combine(root, "a", "b"), full);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../..")]
    [InlineData("a/../../..")]
    public void Resolve_Rejects_Escape(string relative)
    {
        Assert.Throws<DomainException>(() => FileServerPaths.Resolve(root, relative));
    }

    [Fact]
    public void Resolve_Rejects_Absolute_Outside_Root()
    {
        Assert.Throws<DomainException>(() => FileServerPaths.Resolve(root, "C:\\Windows"));
    }

    [Fact]
    public void Resolve_Rejects_Sibling_With_Shared_Prefix()
    {
        var sibling = root + "-evil";
        Assert.Throws<DomainException>(() => FileServerPaths.Resolve(root, sibling));
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("bad:name")]
    public void ValidateSegment_Rejects_Bad_Names(string name)
    {
        var candidate = name;
        if (OperatingSystem.IsWindows() && name == "bad:name")
        {
            Assert.Throws<DomainException>(() => FileServerPaths.ValidateSegment(candidate));
            return;
        }

        if (name is "" or "." or ".." or "a/b" or "a\\b")
        {
            Assert.Throws<DomainException>(() => FileServerPaths.ValidateSegment(candidate));
        }
    }

    [Fact]
    public async Task Service_Roundtrip_List_Upload_Delete()
    {
        var service = CreateService(quotaMaxBytes: 1024 * 1024);

        var listing = service.List(null);
        Assert.Empty(listing.Entries);

        service.CreateDirectory(null, "docs");
        await service.SaveAsync(null, "hello.txt", new MemoryStream("hi"u8.ToArray()), 2, false, CancellationToken.None);

        listing = service.List(null);
        Assert.Equal(2, listing.Entries.Count);
        Assert.Equal("docs", listing.Entries[0].Name);

        service.Delete("hello.txt", false);
        listing = service.List(null);
        Assert.Single(listing.Entries);
    }

    [Fact]
    public async Task Service_Rejects_Overwrite_Without_Flag()
    {
        var service = CreateService(quotaMaxBytes: 1024 * 1024);
        await service.SaveAsync(null, "a.txt", new MemoryStream("hi"u8.ToArray()), 2, false, CancellationToken.None);

        await Assert.ThrowsAsync<SharedFileConflictException>(
            () => service.SaveAsync(null, "a.txt", new MemoryStream("hi"u8.ToArray()), 2, false, CancellationToken.None));
    }

    [Fact]
    public async Task Service_Enforces_Directory_Quota()
    {
        var service = CreateService(quotaMaxBytes: 10);
        await service.SaveAsync(null, "small.bin", new MemoryStream(new byte[8]), 8, false, CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(
            () => service.SaveAsync(null, "big.bin", new MemoryStream(new byte[8]), 8, false, CancellationToken.None));

        // partial upload must not linger
        Assert.False(File.Exists(Path.Combine(root, "big.bin")));
    }

    [Fact]
    public void Service_Rejects_Root_Delete_And_Self_Move()
    {
        var service = CreateService(quotaMaxBytes: 1024 * 1024);
        service.CreateDirectory(null, "sub");

        Assert.Throws<DomainException>(() => service.Delete("", false));
        Assert.Throws<DomainException>(() => service.Move("sub", "sub/inner"));
    }

    [Fact]
    public void Service_List_Escapes_Throw()
    {
        var service = CreateService(quotaMaxBytes: 1024 * 1024);
        Assert.Throws<DomainException>(() => service.List(".."));
    }

    private FileSystemFileService CreateService(long quotaMaxBytes)
    {
        var options = Options.Create(new FileServerOptions
        {
            Enabled = true,
            RootPath = root,
            QuotaMaxBytes = quotaMaxBytes,
            HideSystemFiles = true,
        });
        return new FileSystemFileService(options, NullLogger<FileSystemFileService>.Instance);
    }
}
