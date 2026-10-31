namespace MyHomeLab.Application.Abstractions;

public record AuthPolicy(TimeSpan SessionLifetime, TimeSpan TouchInterval, TimeSpan ReissueInterval);
