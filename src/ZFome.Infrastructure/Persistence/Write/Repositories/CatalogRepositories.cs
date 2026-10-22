using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZFome.Application.Abstractions.Catalog;
using ZFome.Application.Exceptions;
using ZFome.Domain.Catalog;

namespace ZFome.Infrastructure.Persistence.Write.Repositories;

/// <summary>
/// Representa um repositório de categorias que fornece métodos para obter, adicionar e salvar alterações de categorias no banco de dados.
/// </summary>
public sealed class CategoryRepository(ZFomeDbContext context) : ICategoryRepository, ICategoryHierarchyGateway
{
    public Task<Category?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) => context.Categories.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Id == id, cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken)
    { await context.Categories.AddAsync(category, cancellationToken); await CatalogPersistence.ExecuteAsync(() => context.SaveChangesAsync(cancellationToken)); }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => CatalogPersistence.ExecuteAsync(() => context.SaveChangesAsync(cancellationToken));

    public async Task<IReadOnlySet<Guid>> GetAncestorIdsAsync(Guid tenantId, Guid establishmentId, Guid categoryId, CancellationToken cancellationToken)
    {
        var result = new HashSet<Guid>(); var current = categoryId;
        while (true)
        {
            var node = await context.Categories.AsNoTracking().Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Id == current).Select(x => new { x.Id, x.ParentCategoryId }).SingleOrDefaultAsync(cancellationToken);
            if (node?.ParentCategoryId is not { } parent || !result.Add(parent)) return result;
            current = parent;
        }
    }
}

public sealed class ProductRepository(ZFomeDbContext context) : IProductRepository
{
    public Task<Product?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) => context.Products.Include(x => x.Images).Include(x => x.Variations).Include(x => x.AdditionalGroups).SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid tenantId, Guid establishmentId, string code, Guid? exceptProductId, CancellationToken cancellationToken) => context.Products.AnyAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Code == code && (!exceptProductId.HasValue || x.Id != exceptProductId.Value), cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    { await context.Products.AddAsync(product, cancellationToken); await CatalogPersistence.ExecuteAsync(() => context.SaveChangesAsync(cancellationToken)); }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => CatalogPersistence.ExecuteAsync(() => context.SaveChangesAsync(cancellationToken));
}

public sealed class AdditionalRepository(ZFomeDbContext context) : IAdditionalRepository
{
    public Task<Additional?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) => context.Additionals.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Additional>> GetManyAsync(Guid tenantId, Guid establishmentId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => await context.Additionals.Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && ids.Contains(x.Id)).ToListAsync(cancellationToken);

    public async Task AddAsync(Additional additional, CancellationToken cancellationToken)
    { await context.Additionals.AddAsync(additional, cancellationToken); await CatalogPersistence.ExecuteAsync(() => context.SaveChangesAsync(cancellationToken)); }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => CatalogPersistence.ExecuteAsync(() => context.SaveChangesAsync(cancellationToken));
}

public sealed class AdditionalGroupRepository(ZFomeDbContext context) : IAdditionalGroupRepository
{
    public Task<AdditionalGroup?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) => context.AdditionalGroups.Include(x => x.Items).Include(x => x.CompatibilityRules).SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AdditionalGroup>> GetManyAsync(Guid tenantId, Guid establishmentId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => await context.AdditionalGroups.Include(x => x.Items).Include(x => x.CompatibilityRules).Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && ids.Contains(x.Id)).ToListAsync(cancellationToken);

    public async Task AddAsync(AdditionalGroup group, CancellationToken cancellationToken)
    { await context.AdditionalGroups.AddAsync(group, cancellationToken); await CatalogPersistence.ExecuteAsync(() => context.SaveChangesAsync(cancellationToken)); }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => CatalogPersistence.ExecuteAsync(() => context.SaveChangesAsync(cancellationToken));
}

internal static class CatalogPersistence
{
    public static async Task ExecuteAsync(Func<Task<int>> action)
    {
        try { await action(); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { throw new ConflictException("Catalog data conflicts with an existing record."); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.CheckViolation }) { throw new ConflictException("Catalog data violates relational integrity."); }
    }
}
