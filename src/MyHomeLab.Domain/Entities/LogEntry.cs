namespace MyHomeLab.Domain.Entities;

public class LogEntry
{
    private const int MaxApplicationLength = 255;

    public int Id { get; private set; }
    public string Application { get; private set; } = string.Empty;
    public string Log { get; private set; } = string.Empty;

    private LogEntry()
    {
    }

    public static LogEntry Create(string application, string log)
    {
        return new LogEntry
        {
            Application = NormalizeApplication(application),
            Log = log ?? string.Empty,
        };
    }

    internal static LogEntry Restore(int id, string application, string log)
    {
        return new LogEntry
        {
            Id = id,
            Application = application,
            Log = log,
        };
    }

    private static string NormalizeApplication(string? application)
    {
        var trimmed = application?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new DomainException("Application is required.");
        }

        if (trimmed.Length > MaxApplicationLength)
        {
            throw new DomainException("Application cannot exceed 255 characters.");
        }

        return trimmed;
    }
}
