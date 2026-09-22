namespace MyHomeLab.Application.Dtos;

public record DockerContainerDto(
    string Id,
    string Name,
    string Image,
    string State,
    string Status,
    string? Ports,
    DateTime CreatedAt);

public record DockerActionRequest(string Action);

public record DockerActionResultDto(
    string Container,
    string Action,
    bool Success,
    string? Status,
    string? State,
    string? Message);
