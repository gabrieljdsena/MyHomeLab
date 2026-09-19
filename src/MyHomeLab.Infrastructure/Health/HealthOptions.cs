namespace MyHomeLab.Infrastructure.Health;

public sealed class HealthOptions
{
    public int TimeoutSeconds { get; init; } = 3;
}