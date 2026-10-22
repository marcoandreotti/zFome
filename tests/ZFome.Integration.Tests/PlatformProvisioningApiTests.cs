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
using ZFome.Contracts.Platform;
using ZFome.Domain.Identity;
using ZFome.Infrastructure.Persistence.Write;

namespace ZFome.Integration.Tests;

public sealed class PlatformProvisioningApiTests(UserManagementDatabase database) : IClassFixture<UserManagementDatabase>
{
    [Fact]
    public async Task Platform_provisions_atomically_retries_and_rejects_tenant_identity()
    {
        var platform = PlatformUser.Create(new Email($"platform-{Guid.NewGuid():N}@test.local"), "hash", DateTimeOffset.UtcNow);
        await using (var setup = new ZFomeDbContext(database.Options)) { setup.Add(platform); await setup.SaveChangesAsync(); }
        await using var factory = new Factory(database, platform.Id);
        using var client = factory.CreateClient();
        Authenticate(client, "platform");
        var suffix = Guid.NewGuid().ToString("N");
        var request = new ProvisionTenantRequest(Guid.NewGuid(), $"Tenant {suffix}", $"TEN-{suffix[..8]}",
            "Main", $"main-{suffix}", "America/Sao_Paulo", "Owner", $"owner-{suffix}@test.local", "temporary-password");
        var firstResponse = await client.PostAsJsonAsync("/api/platform/tenants", request);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<PlatformProvisioningResponse>();
        var retry = await client.PostAsJsonAsync("/api/platform/tenants", request);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var repeated = await retry.Content.ReadFromJsonAsync<PlatformProvisioningResponse>();
        Assert.Equal(first!.TenantId, repeated!.TenantId);
        Assert.Equal(first.EstablishmentId, repeated.EstablishmentId);
        await using (var verification = new ZFomeDbContext(database.Options))
        {
            Assert.Single(await verification.Tenants.Where(x => x.Id == first.TenantId).ToListAsync());
            Assert.Single(await verification.Establishments.Where(x => x.Id == first.EstablishmentId).ToListAsync());
            var owner = await verification.AdministrativeUsers.Include(x => x.RoleMemberships).Include(x => x.EstablishmentAccesses).SingleAsync(x => x.Id == first.OwnerId);
            Assert.True(owner.PasswordChangeRequired); Assert.True(owner.HasRole(AdministrativeRole.Owner)); Assert.Single(owner.EstablishmentAccesses);
        }
        var page = await client.GetFromJsonAsync<PlatformTenantPageResponse>("/api/platform/tenants?search=" + request.TenantPublicCode);
        Assert.Equal(first.TenantId, Assert.Single(page!.Items).Id);
        Authenticate(client, "tenant");
        var denied = await client.PostAsJsonAsync("/api/platform/tenants", request with { IntentKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private static void Authenticate(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Remove("Cookie"); client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("Cookie", $"zf_access={token}; zf_csrf=test");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", "test");
    }

    private sealed class Factory(UserManagementDatabase database, Guid platformId) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = database.Database.GetConnectionString() }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ZFomeDbContext>(); services.AddScoped(_ => new ZFomeDbContext(database.Options));
                services.RemoveAll<IAuthenticationSessionResolver>(); services.AddSingleton<IAuthenticationSessionResolver>(new Resolver(platformId));
            });
        }
    }

    private sealed class Resolver(Guid platformId) : IAuthenticationSessionResolver
    {
        public Task<AuthenticatedIdentity?> ResolveAsync(string token, CancellationToken ct) => Task.FromResult<AuthenticatedIdentity?>(token switch
        {
            "platform" => new(Guid.NewGuid(), AuthenticationIdentityType.PlatformUser, platformId, null, [], [], false),
            "tenant" => new(Guid.NewGuid(), AuthenticationIdentityType.AdministrativeUser, Guid.NewGuid(), Guid.NewGuid(), [AdministrativeRole.Owner], [], false),
            _ => null
        });
    }
}
