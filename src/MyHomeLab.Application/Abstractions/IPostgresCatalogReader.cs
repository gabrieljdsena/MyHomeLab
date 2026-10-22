using MyHomeLab.Application.Dtos;

namespace MyHomeLab.Application.Abstractions;

public interface IPostgresCatalogReader
{
    /// <summary>
    /// Lists the connectable, non-template databases and, for each, its relations
    /// (tables, views, materialized views) in non-system schemas. A database that
    /// cannot be read contributes an empty relation list instead of failing the
    /// whole catalog.
    /// </summary>
    Task<PostgresCatalogDto> ReadAsync(CancellationToken cancellationToken = default);
}
