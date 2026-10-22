using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZFome.Application.Abstractions.Identity;
using ZFome.Contracts.Administration;
using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence.Write;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class UserManagementDatabase : IAsyncLifetime
{
    public PostgreSqlContainer Database { get; } = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public DbContextOptions<ZFomeDbContext> Options => new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(Database.GetConnectionString()).Options;
    public async Task InitializeAsync()
    {
        await Database.StartAsync();
        await using var db = new ZFomeDbContext(Options);
        await db.Database.EnsureCreatedAsync();
    }
    public async Task DisposeAsync() => await Database.DisposeAsync();
}

public sealed class AdministrativeUserManagementTests(UserManagementDatabase database) : IClassFixture<UserManagementDatabase>
{
    private async Task<Scenario> SeedAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("Management", now);
        var unit = Establishment.Create(tenant.Id, "Main", new Slug("unit-" + Guid.NewGuid().ToString("N")), now);
        AdministrativeUser User(string name, AdministrativeRole role)
        {
            var user = AdministrativeUser.Create(tenant.Id, name, new Email(name + "@example.test"), "hash", role, now);
            user.GrantEstablishmentAccess(unit.Id, tenant.Id, now);
            return user;
        }
        var owner = User("Owner", AdministrativeRole.Owner);
        var other = User("Other", AdministrativeRole.Owner);
        other.GrantRole(AdministrativeRole.Admin, now);
        var admin = User("Admin", AdministrativeRole.Admin);
        var manager = User("Manager", AdministrativeRole.Manager);
        var platform = PlatformUser.Create(new Email(Guid.NewGuid().ToString("N") + "@example.test"), "hash", now);
        platform.ChangePassword("definitive", now);
        await using var db = new ZFomeDbContext(database.Options);
        db.AddRange(tenant, unit, owner, other, admin, manager, platform);
        await db.SaveChangesAsync();
        return new(tenant.Id, unit.Id, owner.Id, other.Id, admin.Id, manager.Id, platform.Id);
    }

    [Theory]
    [InlineData("self-role")]
    [InlineData("grant-owner")]
    [InlineData("remove-owner")]
    [InlineData("disable-owner")]
    [InlineData("enable-owner")]
    [InlineData("create-owner")]
    public async Task Admin_cannot_manage_owner_even_by_direct_http(string action)
    {
        var s = await SeedAsync();
        if (action == "enable-owner")
        {
            await using var db = new ZFomeDbContext(database.Options);
            (await db.AdministrativeUsers.SingleAsync(u => u.Id == s.Other)).Deactivate(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        await using var factory = new Factory(database, s);
        using var client = factory.Client(s.Admin);
        using var response = action switch
        {
            "self-role" => await client.PutAsJsonAsync(s.Path + $"/{s.Admin}/roles/1", new { granted = true }),
            "grant-owner" => await client.PutAsJsonAsync(s.Path + $"/{s.Manager}/roles/1", new { granted = true }),
            "remove-owner" => await client.PutAsJsonAsync(s.Path + $"/{s.Other}/roles/1", new { granted = false }),
            "disable-owner" => await client.PatchAsJsonAsync(s.Path + $"/{s.Other}/active", new { isActive = false }),
            "enable-owner" => await client.PatchAsJsonAsync(s.Path + $"/{s.Other}/active", new { isActive = true }),
            _ => await client.PostAsJsonAsync(s.Path, new AdministrativeUserCreateRequest("New", "new@example.test", "secure-password", 1))
        };
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("traceId", await response.Content.ReadAsStringAsync());
        await using var verify = new ZFomeDbContext(database.Options);
        Assert.Equal(4, await verify.AdministrativeUsers.CountAsync(u => u.TenantId == s.Tenant));
        var admin = await verify.AdministrativeUsers.SingleAsync(u => u.Id == s.Admin);
        Assert.False(AdministrativeUserManagementRules.IsOwner(admin));
        var owner = await verify.AdministrativeUsers.SingleAsync(u => u.Id == s.Other);
        Assert.True(AdministrativeUserManagementRules.IsOwner(owner));
        Assert.Equal(action != "enable-owner", owner.IsActive);
    }

    [Fact]
    public async Task Owner_can_manage_other_owner_but_not_itself()
    {
        var s = await SeedAsync();
        await using var factory = new Factory(database, s);
        using var client = factory.Client(s.Owner);
        using var self = await client.PatchAsJsonAsync(s.Path + $"/{s.Owner}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        using var selfRole = await client.PutAsJsonAsync(s.Path + $"/{s.Owner}/roles/1", new { granted = false });
        Assert.Equal(HttpStatusCode.Forbidden, selfRole.StatusCode);
        using var disable = await client.PatchAsJsonAsync(s.Path + $"/{s.Other}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);
        using var enable = await client.PatchAsJsonAsync(s.Path + $"/{s.Other}/active", new { isActive = true });
        Assert.Equal(HttpStatusCode.NoContent, enable.StatusCode);
        using var remove = await client.PutAsJsonAsync(s.Path + $"/{s.Other}/roles/1", new { granted = false });
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        using var grant = await client.PutAsJsonAsync(s.Path + $"/{s.Other}/roles/1", new { granted = true });
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);
    }

    [Fact]
    public async Task Concurrent_platform_operations_cannot_remove_last_owner()
    {
        var s = await SeedAsync();
        await using var factory = new Factory(database, s);
        using var client = factory.Client(s.Platform);
        var responses = await Task.WhenAll(
            client.PatchAsJsonAsync(s.Path + $"/{s.Owner}/active", new { isActive = false }),
            client.PatchAsJsonAsync(s.Path + $"/{s.Other}/active", new { isActive = false }));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        await using var db = new ZFomeDbContext(database.Options);
        Assert.Equal(1, await db.AdministrativeUsers.CountAsync(u => u.TenantId == s.Tenant && u.IsActive && u.RoleMemberships.Any(r => r.Role == AdministrativeRole.Owner)));
    }

    [Fact]
    public async Task Concurrent_owners_revalidate_actor_after_waiting_for_tenant_lock()
    {
        var s = await SeedAsync();
        await using var factory = new Factory(database, s);
        using var first = factory.Client(s.Owner);
        using var second = factory.Client(s.Other);
        var responses = await Task.WhenAll(
            first.PatchAsJsonAsync(s.Path + $"/{s.Other}/active", new { isActive = false }),
            second.PatchAsJsonAsync(s.Path + $"/{s.Owner}/active", new { isActive = false }));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Forbidden);
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task Search_create_profile_access_and_validation_are_tenant_scoped()
    {
        var s = await SeedAsync(); var foreign = await SeedAsync();
        await using var factory = new Factory(database, s);
        using var client = factory.Client(s.Admin);
        var page = await client.GetFromJsonAsync<AdministrativeUserPageResponse>(s.Path + "?pageSize=2");
        Assert.Equal(4, page!.TotalCount); Assert.Equal(2, page.Items.Count);
        var filtered = await client.GetFromJsonAsync<AdministrativeUserPageResponse>(s.Path + "?search=Owner");
        Assert.Equal(s.Owner, Assert.Single(filtered!.Items).Id);
        using var invalid = await client.PostAsJsonAsync(s.Path, new AdministrativeUserCreateRequest("", "invalid", "short", 99));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var created = await client.PostAsJsonAsync(s.Path, new AdministrativeUserCreateRequest("New", "new@example.test", "secure-password", 3));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var duplicate = await client.PostAsJsonAsync(s.Path, new AdministrativeUserCreateRequest("New", "NEW@example.test", "secure-password", 3));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var profile = await client.PutAsJsonAsync(s.Path + $"/{s.Manager}", new { name = "Updated" });
        Assert.Equal(HttpStatusCode.NoContent, profile.StatusCode);
        using var revoke = await client.PutAsJsonAsync(s.Path + $"/{s.Manager}/access", new { granted = false });
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        var associated = await client.GetFromJsonAsync<AdministrativeUserPageResponse>(s.Path + "?associatedOnly=true");
        Assert.DoesNotContain(associated!.Items, u => u.Id == s.Manager);
        using var grant = await client.PutAsJsonAsync(s.Path + $"/{s.Manager}/access", new { granted = true });
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);
        using var crossed = await client.PutAsJsonAsync(s.Path + $"/{foreign.Admin}", new { name = "Crossed" });
        Assert.Equal(HttpStatusCode.NotFound, crossed.StatusCode);
        using var crossedUnit = await client.GetAsync(foreign.Path);
        Assert.Equal(HttpStatusCode.Forbidden, crossedUnit.StatusCode);
        using var manager = factory.Client(s.Manager);
        using var denied = await manager.GetAsync(s.Path);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Platform_cannot_remove_last_owner_role_and_last_administrator_keeps_unit_access()
    {
        var s = await SeedAsync();
        await using (var db = new ZFomeDbContext(database.Options))
        {
            var users = await db.AdministrativeUsers.Where(u => u.TenantId == s.Tenant).ToListAsync();
            foreach (var user in users.Where(u => u.Id != s.Owner)) user.Deactivate(DateTimeOffset.UtcNow);
            users.Single(u => u.Id == s.Owner).GrantRole(AdministrativeRole.Admin, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        await using var factory = new Factory(database, s);
        using var platform = factory.Client(s.Platform);
        using var role = await platform.PutAsJsonAsync(s.Path + $"/{s.Owner}/roles/1", new { granted = false });
        Assert.Equal(HttpStatusCode.Conflict, role.StatusCode);
        using var owner = factory.Client(s.Owner);
        using var access = await owner.PutAsJsonAsync(s.Path + $"/{s.Owner}/access", new { granted = false });
        Assert.Equal(HttpStatusCode.Conflict, access.StatusCode);
        await using var verify = new ZFomeDbContext(database.Options);
        var persisted = await verify.AdministrativeUsers.SingleAsync(u => u.Id == s.Owner);
        Assert.True(persisted.HasRole(AdministrativeRole.Owner));
        Assert.True(Assert.Single(persisted.EstablishmentAccesses).IsActive);
    }

    private sealed record Scenario(Guid Tenant, Guid Unit, Guid Owner, Guid Other, Guid Admin, Guid Manager, Guid Platform)
    {
        public string Path => $"/api/admin/establishments/{Unit}/users";
    }

    private sealed class Factory(UserManagementDatabase database, Scenario scenario) : WebApplicationFactory<Program>
    {
        public HttpClient Client(Guid actor)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Add("Cookie", $"zf_access={actor}; zf_csrf=test");
            client.DefaultRequestHeaders.Add("X-CSRF-Token", "test");
            return client;
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = database.Database.GetConnectionString() }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ZFomeDbContext>();
                services.AddScoped(_ => new ZFomeDbContext(database.Options));
                services.RemoveAll<IAuthenticationSessionResolver>();
                services.AddSingleton<IAuthenticationSessionResolver>(new Resolver(scenario));
            });
        }
    }

    // Simula apenas a sessão; autorização vigente, CQRS, SQL e transações são reais.
    private sealed class Resolver(Scenario s) : IAuthenticationSessionResolver
    {
        public Task<AuthenticatedIdentity?> ResolveAsync(string token, CancellationToken ct)
        {
            if (!Guid.TryParse(token, out var actor)) return Task.FromResult<AuthenticatedIdentity?>(null);
            var platform = actor == s.Platform;
            var role = actor == s.Admin ? AdministrativeRole.Admin : actor == s.Manager ? AdministrativeRole.Manager : AdministrativeRole.Owner;
            return Task.FromResult<AuthenticatedIdentity?>(new(Guid.NewGuid(), platform ? AuthenticationIdentityType.PlatformUser : AuthenticationIdentityType.AdministrativeUser, actor, platform ? null : s.Tenant, platform ? [] : [role], [s.Unit], false));
        }
    }
}
