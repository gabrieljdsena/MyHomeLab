namespace MyHomeLab.Application.Dtos;

public record ToolInfo(
    string Name,
    bool Found,
    string? Path,
    string? Version);
