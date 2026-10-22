using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZFome.Application.Abstractions.Catalog;
using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Catalog;
using ZFome.Contracts.Administration;
using ZFome.Contracts.Catalog;
using ZFome.Domain.Catalog;
using ZFome.Domain.Identity;
using ZFome.Domain.SharedKernel;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence.Write;

namespace ZFome.Integration.Tests;

public sealed class CatalogMaintenanceTests(UserManagementDatabase database) : IClassFixture<UserManagementDatabase>
{
    [Fact]
    public async Task Independent_queries_include_unlinked_inactive_resources_and_preserve_public_contract()
    {
        await using var db = new ZFomeDbContext(database.Options);
        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("Catalog", now);
        var otherTenant = Tenant.Create("Other", now);
        var slug = "catalog-" + Guid.NewGuid().ToString("N");
        var unit = Establishment.Create(tenant.Id, "Unit", new Slug(slug), now);
        var otherUnit = Establishment.Create(tenant.Id, "Other unit", new Slug("other-" + Guid.NewGuid().ToString("N")), now);
        var foreignUnit = Establishment.Create(otherTenant.Id, "Foreign", new Slug("foreign-" + Guid.NewGuid().ToString("N")), now);
        var actor = AdministrativeUser.Create(tenant.Id, "Manager", new Email("manager@example.test"), "hash", AdministrativeRole.Manager, now);
        actor.GrantEstablishmentAccess(unit.Id, tenant.Id, now);
        var category = Category.Create(tenant.Id, unit.Id, "Main");
        var product = Product.Create(tenant.Id, unit.Id, category, "P1", "Product", new Money(10));
        var active = Additional.Create(tenant.Id, unit.Id, "Active", new Money(2));
        var inactive = Additional.Create(tenant.Id, unit.Id, "Inactive", new Money(3)); inactive.Deactivate();
        var linked = AdditionalGroup.Create(tenant.Id, unit.Id, "Linked", 0, 2);
        linked.AddItem(inactive, 1); linked.AddItem(active, 2); product.LinkAdditionalGroup(linked, 0);
        var orphan = AdditionalGroup.Create(tenant.Id, unit.Id, "Orphan", 0, 2);
        orphan.AddItem(inactive, 5); orphan.Deactivate();
        db.AddRange(tenant, otherTenant, unit, otherUnit, foreignUnit, actor, category, product, active, inactive, linked, orphan);
        for (var i = 0; i < 23; i++)
            db.Add(Additional.Create(tenant.Id, unit.Id, $"Unlinked {i:00}", Money.Zero));
        db.AddRange(Additional.Create(tenant.Id, otherUnit.Id, "Hidden same tenant", Money.Zero), Additional.Create(otherTenant.Id, foreignUnit.Id, "Hidden foreign", Money.Zero));
        await db.SaveChangesAsync();
        await using var factory = new Factory(database, actor, unit.Id);
        using var client = factory.CreateClient();
        var publicPath = $"/api/public/establishments/{slug}/catalog";
        var before = await client.GetStringAsync(publicPath);
        var publicMenu = await client.GetFromJsonAsync<CatalogResponse>(publicPath);
        Assert.Equal(active.Id, Assert.Single(Assert.Single(Assert.Single(Assert.Single(publicMenu!.Categories).Products).AdditionalGroups).Items).Id);
        client.DefaultRequestHeaders.Add("Cookie", "zf_access=manager; zf_csrf=test");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", "test");
        var root = $"/api/admin/establishments/{unit.Id}/catalog";
        var first = await client.GetFromJsonAsync<PagedResponse<AdditionalResponse>>(root + "/additionals?pageSize=20");
        var second = await client.GetFromJsonAsync<PagedResponse<AdditionalResponse>>(root + "/additionals?pageSize=20&page=2");
        var repeat = await client.GetFromJsonAsync<PagedResponse<AdditionalResponse>>(root + "/additionals?pageSize=20&page=2");
        Assert.Equal(25, first!.Total); Assert.Equal(20, first.Items.Count); Assert.Equal(5, second!.Items.Count);
        Assert.Equal(second.Items.Select(x => x.Id), repeat!.Items.Select(x => x.Id));
        Assert.Equal(25, first.Items.Concat(second.Items).Select(x => x.Id).Distinct().Count());
        var filtered = await client.GetFromJsonAsync<PagedResponse<AdditionalResponse>>(root + "/additionals?isActive=false&search=inactive");
        Assert.Equal(inactive.Id, Assert.Single(filtered!.Items).Id);
        var groups = await client.GetFromJsonAsync<PagedResponse<AdditionalGroupResponse>>(root + "/additional-groups");
        Assert.Equal(2, groups!.Total);
        var orphanResult = Assert.Single(groups.Items, group => group.Id == orphan.Id);
        Assert.False(orphanResult.IsActive); Assert.False(Assert.Single(orphanResult.Items).IsActive);
        Assert.Equal(5, orphanResult.Items[0].Order);
        Assert.Equal(new[] { 1, 2 }, groups.Items.Single(x => x.Id == linked.Id).Items.Select(x => x.Order));
        var groupFilter = await client.GetFromJsonAsync<PagedResponse<AdditionalGroupResponse>>(root + "/additional-groups?isActive=false&search=orphan&pageSize=1");
        Assert.Equal(orphan.Id, Assert.Single(groupFilter!.Items).Id);
        var groupSecondPage = await client.GetFromJsonAsync<PagedResponse<AdditionalGroupResponse>>(root + "/additional-groups?pageSize=1&page=2");
        Assert.Equal(orphan.Id, Assert.Single(groupSecondPage!.Items).Id);
        using var edited = await client.PutAsJsonAsync(root + $"/additional-groups/{orphan.Id}", new UpsertAdditionalGroupRequest(orphan.Name, 0, 2, false, [new(inactive.Id, 5)]));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var reread = await client.GetFromJsonAsync<PagedResponse<AdditionalGroupResponse>>(root + "/additional-groups?search=orphan");
        Assert.Equal(inactive.Id, Assert.Single(Assert.Single(reread!.Items).Items).Id);
        using var created = await client.PostAsJsonAsync(root + "/additionals", new UpsertAdditionalRequest("New standalone", 5, true));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var newItem = await client.GetFromJsonAsync<PagedResponse<AdditionalResponse>>(root + "/additionals?search=New%20standalone");
        Assert.Equal("New standalone", Assert.Single(newItem!.Items).Name);
        foreach (var suffix in new[] { "additionals?page=0", "additional-groups?pageSize=101", "additionals?pageSize=0" })
        {
            using var invalid = await client.GetAsync(root + "/" + suffix);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Contains("errors", await invalid.Content.ReadAsStringAsync());
        }
        foreach (var id in new[] { otherUnit.Id, foreignUnit.Id })
        {
            using var denied = await client.GetAsync($"/api/admin/establishments/{id}/catalog/additionals");
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using (var scope = factory.Services.CreateScope())
        {
            var gateway = scope.ServiceProvider.GetRequiredService<ICatalogMaintenanceReadGateway>();
            Assert.Empty((await gateway.SearchAdditionalsAsync(otherTenant.Id, new(unit.Id), CancellationToken.None)).Items);
            Assert.Empty((await gateway.SearchGroupsAsync(otherTenant.Id, new(unit.Id), CancellationToken.None)).Items);
        }
        client.DefaultRequestHeaders.Remove("Cookie");
        Assert.Equal(before, await client.GetStringAsync(publicPath));
        using var unauthenticated = await client.GetAsync(root + "/additionals");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        client.DefaultRequestHeaders.Add("Cookie", "zf_access=kitchen");
        using var forbidden = await client.GetAsync(root + "/additional-groups");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    private sealed class Factory(UserManagementDatabase database, AdministrativeUser actor, Guid unitId) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = database.Database.GetConnectionString() }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ZFomeDbContext>(); services.AddScoped(_ => new ZFomeDbContext(database.Options));
                services.RemoveAll<IAuthenticationSessionResolver>(); services.AddSingleton<IAuthenticationSessionResolver>(new Resolver(actor, unitId));
            });
        }
    }
    private sealed class Resolver(AdministrativeUser actor, Guid unitId) : IAuthenticationSessionResolver
    {
        public Task<AuthenticatedIdentity?> ResolveAsync(string token, CancellationToken ct) => Task.FromResult<AuthenticatedIdentity?>(
            token is "manager" or "kitchen" ? new(Guid.NewGuid(), AuthenticationIdentityType.AdministrativeUser, actor.Id, actor.TenantId, [token == "manager" ? AdministrativeRole.Manager : AdministrativeRole.Kitchen], [unitId], false) : null);
    }
}
