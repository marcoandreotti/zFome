namespace ZFome.Application.Abstractions.Catalog;

public interface ICategoryHierarchyGateway
{
    Task<IReadOnlySet<Guid>> GetAncestorIdsAsync(Guid tenantId, Guid establishmentId, Guid categoryId, CancellationToken cancellationToken);
}
