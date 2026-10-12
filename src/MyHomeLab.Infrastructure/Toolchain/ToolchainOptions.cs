namespace MyHomeLab.Infrastructure.Toolchain;

public sealed class ToolchainOptions
{
    public string? NodeExecutable { get; set; }

    public string? PythonExecutable { get; set; }

    public string? GitExecutable { get; set; }

    public string? SevenZipExecutable { get; set; }

    public string? FfmpegExecutable { get; set; }

    public string? OpencodeExecutable { get; set; }
}
