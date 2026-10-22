using ZFome.Domain.Catalog;

namespace ZFome.Application.Abstractions.Catalog;

public interface ICategoryRepository
{
    Task<Category?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken);
    Task AddAsync(Category category, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IProductRepository
{
    Task<Product?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(Guid tenantId, Guid establishmentId, string code, Guid? exceptProductId, CancellationToken cancellationToken);
    Task AddAsync(Product product, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IAdditionalRepository
{
    Task<Additional?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Additional>> GetManyAsync(Guid tenantId, Guid establishmentId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task AddAsync(Additional additional, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IAdditionalGroupRepository
{
    Task<AdditionalGroup?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdditionalGroup>> GetManyAsync(Guid tenantId, Guid establishmentId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task AddAsync(AdditionalGroup group, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IOfferAvailabilityRepository
{
    Task<bool> OfferExistsAsync(Guid tenantId, Guid establishmentId, OfferKind kind, Guid offerId, CancellationToken cancellationToken);
    Task<OfferUnavailability?> GetActiveAsync(Guid tenantId, Guid establishmentId, OfferKind kind, Guid offerId, DateTimeOffset instant, CancellationToken cancellationToken);
    void Add(OfferUnavailability value);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ICatalogReadGateway
{
    Task<CatalogReadModel?> GetAdministrativeAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken);
    Task<CatalogReadModel?> GetPublicAsync(string normalizedSlug, CancellationToken cancellationToken);
}

public sealed record CatalogReadModel(Guid EstablishmentId, string EstablishmentName, string Slug, IReadOnlyList<CategoryReadModel> Categories);
public sealed record CategoryReadModel(Guid Id, Guid? ParentId, string Name, string? Description, int Order, string? ImageUrl, bool IsActive, IReadOnlyList<ProductReadModel> Products);
public sealed record ProductReadModel(Guid Id, string Code, string Name, string? Description, decimal BasePrice, bool IsFeatured, bool IsActive, bool AllowsNotes, IReadOnlyList<ProductImageReadModel> Images, IReadOnlyList<ProductVariationReadModel> Variations, IReadOnlyList<AdditionalGroupReadModel> AdditionalGroups, bool IsAvailable = true, string? UnavailabilityReason = null, DateTimeOffset? AvailableAgainAt = null, decimal? RatingAverage = null, long RatingCount = 0);
public sealed record ProductImageReadModel(Guid Id, string Url, int Order, bool IsPrincipal);
public sealed record ProductVariationReadModel(Guid Id, string Name, decimal Price, int Order, bool IsActive, bool IsAvailable = true, string? UnavailabilityReason = null, DateTimeOffset? AvailableAgainAt = null);
public sealed record AdditionalGroupReadModel(Guid Id, string Name, int MinimumSelection, int MaximumSelection, bool IsActive, int Order, IReadOnlyList<AdditionalReadModel> Items, string PricingStrategy = "Additive", string Type = "Additional", bool RequiresCompleteComposition = false);
public sealed record AdditionalReadModel(Guid Id, string Name, decimal Price, bool IsActive, int Order, bool IsAvailable = true, string? UnavailabilityReason = null, DateTimeOffset? AvailableAgainAt = null, IReadOnlyList<ModifierCompatibilityReadModel>? CompatibilityRules = null);
public sealed record ModifierCompatibilityReadModel(Guid TargetGroupId, Guid TargetAdditionalId, string Kind);
