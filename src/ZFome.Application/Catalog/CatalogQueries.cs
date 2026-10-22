using ZFome.Application.Abstractions.Catalog;
using ZFome.Application.Abstractions.Queries;

namespace ZFome.Application.Catalog;

public sealed record GetAdministrativeCatalogQuery(Guid EstablishmentId) : IQuery<CatalogReadModel>;
public sealed record GetPublicCatalogQuery(string Slug) : IQuery<CatalogReadModel>;
