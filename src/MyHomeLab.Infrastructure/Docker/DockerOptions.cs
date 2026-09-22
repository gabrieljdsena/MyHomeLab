namespace MyHomeLab.Infrastructure.Docker;

public sealed class DockerOptions
{
    public bool Enabled { get; init; } = true;

    public int TimeoutSeconds { get; init; } = 30;

    public string ExecutablePath { get; init; } = "docker";
}
