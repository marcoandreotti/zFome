using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ZFome.Application;
using ZFome.Application.Abstractions.Catalog;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Catalog;
using ZFome.Application.Exceptions;
using ZFome.Application.Tenancy;
using ZFome.Domain.Catalog;

namespace ZFome.Application.Tests.Catalog;

public sealed class CatalogApplicationTests
{
    [Fact]
    public async Task Category_handler_derives_scope_and_rejects_unauthorized_unit()
    {
        var handler = new UpsertCategoryCommandHandler(new EstablishmentScopeResolver(new TenantContext(), new AccessGateway(false)), new CategoryRepository(), new HierarchyGateway());
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.HandleAsync(new(Guid.NewGuid(), null, "Pizzas", null, 0, null, null, true), CancellationToken.None));
    }

    [Fact]
    public async Task Category_handler_creates_category_in_resolved_scope()
    {
        var repository = new CategoryRepository(); var establishmentId = Guid.NewGuid();
        var handler = new UpsertCategoryCommandHandler(new EstablishmentScopeResolver(new TenantContext(), new AccessGateway(true)), repository, new HierarchyGateway());
        var id = await handler.HandleAsync(new(establishmentId, null, "Pizzas", null, 1, null, null, true), CancellationToken.None);
        Assert.Equal(id, repository.Value!.Id); Assert.Equal(TenantContext.TenantIdValue, repository.Value.TenantId); Assert.Equal(establishmentId, repository.Value.EstablishmentId);
    }

    [Fact]
    public async Task Product_handler_reports_unit_scoped_code_conflict()
    {
        var establishmentId = Guid.NewGuid(); var category = Category.Create(TenantContext.TenantIdValue, establishmentId, "Pizzas");
        var handler = new UpsertProductCommandHandler(new EstablishmentScopeResolver(new TenantContext(), new AccessGateway(true)), new CategoryRepository(category), new ProductRepository(true), new GroupRepository());
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(new(establishmentId, null, category.Id, "P1", "Pizza", null, 10, false, true, true, [], [], []), CancellationToken.None));
    }

    [Fact]
    public async Task Public_query_uses_slug_gateway_without_authenticated_context()
    {
        var expected = new CatalogReadModel(Guid.NewGuid(), "Unit", "unit-a", []); var gateway = new ReadGateway(expected);
        var result = await new GetPublicCatalogQueryHandler(gateway).HandleAsync(new("UNIT-A"), CancellationToken.None);
        Assert.Same(expected, result); Assert.Equal("unit-a", gateway.Slug);
    }

    [Fact]
    public async Task Validators_reject_malformed_catalog_inputs()
    {
        Assert.False((await new UpsertCategoryCommandValidator().ValidateAsync(new UpsertCategoryCommand(Guid.Empty, null, "", null, -1, "relative", null, true))).IsValid);
        Assert.False((await new UpsertProductCommandValidator().ValidateAsync(new UpsertProductCommand(Guid.Empty, null, Guid.Empty, "", "", null, -1, false, true, true, [new("bad", -1, true), new("bad", 0, true)], [], []))).IsValid);
        Assert.False((await new UpsertAdditionalCommandValidator().ValidateAsync(new UpsertAdditionalCommand(Guid.Empty, null, "", -1, true))).IsValid);
        Assert.False((await new UpsertAdditionalGroupCommandValidator().ValidateAsync(new UpsertAdditionalGroupCommand(Guid.Empty, null, "", 2, 1, true, []))).IsValid);
    }

    [Fact]
    public void Application_module_registers_every_catalog_handler_and_validator()
    {
        var services = new ServiceCollection(); services.AddApplication();
        Assert.Equal(16, services.Count(x => x.ImplementationType?.Namespace == typeof(UpsertCategoryCommand).Namespace));
    }

    private sealed class TenantContext : ITenantContext
    {
        public static readonly Guid TenantIdValue = Guid.Parse("11111111-1111-1111-1111-111111111111"); public bool HasTenant => true; public Guid TenantId => TenantIdValue; public bool HasUser => true; public Guid UserId => Guid.Parse("22222222-2222-2222-2222-222222222222"); public Guid GetRequiredTenantId() => TenantId; public Guid GetRequiredUserId() => UserId;
    }
    private sealed class AccessGateway(bool allowed) : IEstablishmentAccessGateway { public Task<bool> HasActiveAccessAsync(Guid tenantId, Guid userId, Guid establishmentId, CancellationToken cancellationToken) => Task.FromResult(allowed); }
    private sealed class HierarchyGateway : ICategoryHierarchyGateway { public Task<IReadOnlySet<Guid>> GetAncestorIdsAsync(Guid tenantId, Guid establishmentId, Guid categoryId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>()); }
    private sealed class CategoryRepository(Category? value = null) : ICategoryRepository { public Category? Value { get; private set; } = value; public Task<Category?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) => Task.FromResult(Value is { } x && x.Id == id ? x : null); public Task AddAsync(Category category, CancellationToken cancellationToken) { Value = category; return Task.CompletedTask; } public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class ProductRepository(bool conflict) : IProductRepository { public Task<Product?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) => Task.FromResult<Product?>(null); public Task<bool> CodeExistsAsync(Guid tenantId, Guid establishmentId, string code, Guid? exceptProductId, CancellationToken cancellationToken) => Task.FromResult(conflict); public Task AddAsync(Product product, CancellationToken cancellationToken) => Task.CompletedTask; public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class GroupRepository : IAdditionalGroupRepository { public Task<AdditionalGroup?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) => Task.FromResult<AdditionalGroup?>(null); public Task<IReadOnlyList<AdditionalGroup>> GetManyAsync(Guid tenantId, Guid establishmentId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AdditionalGroup>>([]); public Task AddAsync(AdditionalGroup group, CancellationToken cancellationToken) => Task.CompletedTask; public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class ReadGateway(CatalogReadModel value) : ICatalogReadGateway { public string? Slug { get; private set; } public Task<CatalogReadModel?> GetAdministrativeAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken) => Task.FromResult<CatalogReadModel?>(value); public Task<CatalogReadModel?> GetPublicAsync(string normalizedSlug, CancellationToken cancellationToken) { Slug = normalizedSlug; return Task.FromResult<CatalogReadModel?>(value); } }
}
