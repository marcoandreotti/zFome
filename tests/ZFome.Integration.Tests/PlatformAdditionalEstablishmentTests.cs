using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZFome.Application.Abstractions.Identity;
using ZFome.Contracts.Platform;
using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence.Write;

namespace ZFome.Integration.Tests;

public sealed class PlatformAdditionalEstablishmentTests(UserManagementDatabase database) : IClassFixture<UserManagementDatabase>
{
    [Fact]
    public async Task Adds_unit_for_owner_of_target_tenant_and_rejects_cross_tenant_owner()
    {
        var now = DateTimeOffset.UtcNow; var suffix = Guid.NewGuid().ToString("N");
        var platform = PlatformUser.Create(new Email($"platform-unit-{suffix}@test.local"), "hash", now);
        var tenant = Tenant.Create("Target", $"TARGET-{suffix[..8]}", now);
        var owner = AdministrativeUser.Create(tenant.Id, "Owner", new Email($"owner-unit-{suffix}@test.local"), "hash", AdministrativeRole.Owner, now);
        var foreignTenant = Tenant.Create("Foreign", $"FOREIGN-{suffix[..8]}", now);
        var foreignOwner = AdministrativeUser.Create(foreignTenant.Id, "Foreign", new Email($"foreign-unit-{suffix}@test.local"), "hash", AdministrativeRole.Owner, now);
        await using (var setup = new ZFomeDbContext(database.Options)) { setup.AddRange(platform, tenant, owner, foreignTenant, foreignOwner); await setup.SaveChangesAsync(); }
        await using var factory = new Factory(database, platform.Id); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "zf_access=platform; zf_csrf=test"); client.DefaultRequestHeaders.Add("X-CSRF-Token", "test");
        var request = new AddPlatformEstablishmentRequest(Guid.NewGuid(), "Branch", $"branch-{suffix}", "America/Sao_Paulo", owner.Id);
        var response = await client.PostAsJsonAsync($"/api/platform/tenants/{tenant.Id}/establishments", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PlatformProvisioningResponse>();
        await using (var verification = new ZFomeDbContext(database.Options))
        {
            var persisted = await verification.Establishments.FindAsync(created!.EstablishmentId); Assert.Equal(tenant.Id, persisted!.TenantId); Assert.Null(persisted.OnboardingCompletedAt);
        }
        var denied = await client.PostAsJsonAsync($"/api/platform/tenants/{tenant.Id}/establishments", request with { IntentKey = Guid.NewGuid(), Slug = $"cross-{suffix}", OwnerId = foreignOwner.Id });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    private sealed class Factory(UserManagementDatabase database, Guid platformId) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = database.Database.GetConnectionString() }));
            builder.ConfigureTestServices(services => { services.RemoveAll<ZFomeDbContext>(); services.AddScoped(_ => new ZFomeDbContext(database.Options)); services.RemoveAll<IAuthenticationSessionResolver>(); services.AddSingleton<IAuthenticationSessionResolver>(new Resolver(platformId)); });
        }
    }
    private sealed class Resolver(Guid platformId) : IAuthenticationSessionResolver
    { public Task<AuthenticatedIdentity?> ResolveAsync(string token, CancellationToken ct) => Task.FromResult<AuthenticatedIdentity?>(token == "platform" ? new(Guid.NewGuid(), AuthenticationIdentityType.PlatformUser, platformId, null, [], [], false) : null); }
}
