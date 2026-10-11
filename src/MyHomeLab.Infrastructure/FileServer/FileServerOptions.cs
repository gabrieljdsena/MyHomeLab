namespace MyHomeLab.Infrastructure.FileServer;

public sealed class FileServerOptions
{
    public bool Enabled { get; set; } = true;

    public string? RootPath { get; set; } = "C:\\Shared-Server";

    public long QuotaMaxBytes { get; set; } = 25L * 1024 * 1024 * 1024;

    public bool HideSystemFiles { get; set; } = true;
}
