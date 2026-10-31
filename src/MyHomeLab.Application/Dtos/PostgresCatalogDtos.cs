namespace MyHomeLab.Application.Dtos;

public sealed record PostgresCatalogDto(string Current, IReadOnlyList<PostgresDatabaseDto> Databases);

public sealed record PostgresDatabaseDto(string Name, IReadOnlyList<PostgresRelationDto> Relations);

public sealed record PostgresRelationDto(string Schema, string Name, string Kind);
