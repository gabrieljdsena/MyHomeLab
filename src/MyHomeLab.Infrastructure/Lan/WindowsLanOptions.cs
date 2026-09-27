namespace MyHomeLab.Infrastructure.Lan;

public sealed class WindowsLanOptions
{
    public bool Enabled { get; init; } = true;

    public int ProbeIntervalSeconds { get; init; } = 30;

    public int ProbeTimeoutMs { get; init; } = 2000;
}
