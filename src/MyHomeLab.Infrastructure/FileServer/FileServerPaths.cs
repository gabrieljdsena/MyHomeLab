using MyHomeLab.Domain;

namespace MyHomeLab.Infrastructure.FileServer;

public static class FileServerPaths
{
    public static string GetRoot(string? configuredRoot)
    {
        var candidate = configuredRoot?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new DomainException("File server root path is not configured.");
        }

        try
        {
            return Path.GetFullPath(candidate);
        }
        catch (Exception ex)
        {
            throw new DomainException($"Invalid file server root path: {ex.Message}");
        }
    }

    public static string NormalizeRelative(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            return string.Empty;
        }

        var normalized = relative.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).Trim();
        normalized = normalized.Trim(Path.DirectorySeparatorChar).Trim();
        if (normalized is "" or ".")
        {
            return string.Empty;
        }

        return normalized;
    }

    public static string Resolve(string root, string? relative)
    {
        var normalized = NormalizeRelative(relative);
        string full;
        try
        {
            full = string.IsNullOrEmpty(normalized)
                ? root
                : Path.GetFullPath(Path.Combine(root, normalized));
        }
        catch (Exception ex)
        {
            throw new DomainException($"Invalid path: {ex.Message}");
        }

        if (!full.Equals(root, StringComparison.OrdinalIgnoreCase)
            && !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Path escapes the shared root.");
        }

        return full;
    }

    public static string ToRelative(string root, string full)
    {
        if (full.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var relative = Path.GetRelativePath(root, full);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    public static void ValidateSegment(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Name is required.");
        }

        var trimmed = name.Trim();
        if (trimmed is "." or "..")
        {
            throw new DomainException("Name is reserved.");
        }

        if (trimmed.Contains('/') || trimmed.Contains('\\'))
        {
            throw new DomainException("Name must not contain path separators.");
        }

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new DomainException($"Name contains invalid characters: '{trimmed}'.");
        }

        if (trimmed.EndsWith('.') || trimmed.EndsWith(' '))
        {
            throw new DomainException("Name must not end with a dot or space.");
        }
    }
}
