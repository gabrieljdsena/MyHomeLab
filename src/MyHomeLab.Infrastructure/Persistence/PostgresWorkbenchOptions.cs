namespace MyHomeLab.Infrastructure.Persistence;

public sealed class PostgresWorkbenchOptions
{
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxRows { get; set; } = 500;
}
