namespace MyHomeLab.Infrastructure.Deploy;

public sealed class DeployOptions
{
    public int TimeoutSeconds { get; set; } = 1800;

    public int OutputTailChars { get; set; } = 8000;

    public string? StateDirectory { get; set; }
}
