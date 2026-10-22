using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZFome.Application.Exceptions;
using ZFome.Domain.Catalog;
using ZFome.Domain.SharedKernel;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence;
using ZFome.Infrastructure.Persistence.Read;
using ZFome.Infrastructure.Persistence.Write;
using ZFome.Infrastructure.Persistence.Write.Repositories;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class CatalogPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Ef_and_Dapper_keep_catalogs_isolated_and_public_projection_active_only()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var context = new ZFomeDbContext(options); await context.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow; var tenantA = Tenant.Create("A", now); var tenantB = Tenant.Create("B", now);
        var unitA = Establishment.Create(tenantA.Id, "Unit A", new Slug("unit-a"), now); var unitB = Establishment.Create(tenantB.Id, "Unit B", new Slug("unit-b"), now);
        context.AddRange(tenantA, tenantB, unitA, unitB); await context.SaveChangesAsync();
        var categoryA = Category.Create(tenantA.Id, unitA.Id, "Pizzas", 0); var categoryB = Category.Create(tenantB.Id, unitB.Id, "Pizzas", 0);
        var productA = Product.Create(tenantA.Id, unitA.Id, categoryA, "P1", "Pizza A", new Money(30)); productA.AddImage("https://example.com/a.jpg", 0, true); productA.AddVariation("Inativa", new Money(40), 0).Deactivate();
        var productB = Product.Create(tenantB.Id, unitB.Id, categoryB, "P1", "Pizza B", new Money(35));
        var inactive = Product.Create(tenantA.Id, unitA.Id, categoryA, "P2", "Inativa", new Money(20)); inactive.Deactivate();
        context.AddRange(categoryA, categoryB, productA, productB, inactive); await context.SaveChangesAsync();
        var gateway = new CatalogReadGateway(new NpgsqlReadConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = database.GetConnectionString() })));
        var admin = await gateway.GetAdministrativeAsync(tenantA.Id, unitA.Id, CancellationToken.None); var publicMenu = await gateway.GetPublicAsync("unit-a", CancellationToken.None);
        Assert.Equal(2, Assert.Single(admin!.Categories).Products.Count); Assert.Single(Assert.Single(publicMenu!.Categories).Products); Assert.Empty(Assert.Single(Assert.Single(publicMenu.Categories).Products).Variations);
        Assert.Null(await gateway.GetAdministrativeAsync(tenantA.Id, unitB.Id, CancellationToken.None)); Assert.Null(await gateway.GetPublicAsync("missing", CancellationToken.None));
    }

    [Fact]
    public async Task Database_rejects_duplicate_code_and_cross_unit_relations()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var context = new ZFomeDbContext(options); await context.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow; var tenant = Tenant.Create("A", now); var otherTenant = Tenant.Create("B", now); var unit = Establishment.Create(tenant.Id, "A", new Slug("unit-a"), now); var otherUnit = Establishment.Create(otherTenant.Id, "B", new Slug("unit-b"), now);
        context.AddRange(tenant, otherTenant, unit, otherUnit); await context.SaveChangesAsync();
        var category = Category.Create(tenant.Id, unit.Id, "A"); var otherCategory = Category.Create(otherTenant.Id, otherUnit.Id, "B"); context.AddRange(category, otherCategory); await context.SaveChangesAsync();
        context.Products.Add(Product.Create(tenant.Id, unit.Id, category, "P1", "One", Money.Zero)); await context.SaveChangesAsync();
        context.Products.Add(Product.Create(tenant.Id, unit.Id, category, "P1", "Duplicate", Money.Zero)); await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();
        context.Products.Add(Product.Create(otherTenant.Id, otherUnit.Id, otherCategory, "P1", "Allowed", Money.Zero)); await context.SaveChangesAsync();
        var invalid = Product.Create(tenant.Id, unit.Id, category, "P2", "Invalid", Money.Zero); typeof(Product).GetProperty(nameof(Product.CategoryId))!.SetValue(invalid, otherCategory.Id); context.Products.Add(invalid);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Repository_translates_duplicate_product_code_to_conflict()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var context = new ZFomeDbContext(options); await context.Database.EnsureCreatedAsync();
        var tenant = Tenant.Create("A", DateTimeOffset.UtcNow); var unit = Establishment.Create(tenant.Id, "A", new Slug("unit-a"), DateTimeOffset.UtcNow); var category = Category.Create(tenant.Id, unit.Id, "A"); context.AddRange(tenant, unit, category); await context.SaveChangesAsync();
        var repository = new ProductRepository(context); await repository.AddAsync(Product.Create(tenant.Id, unit.Id, category, "P1", "One", Money.Zero), CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() => repository.AddAsync(Product.Create(tenant.Id, unit.Id, category, "P1", "Two", Money.Zero), CancellationToken.None));
    }
}
