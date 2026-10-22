using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Onboarding;
using ZFome.Contracts.Administration;
using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence;
using ZFome.Infrastructure.Persistence.Read;
using ZFome.Infrastructure.Persistence.Write;

namespace ZFome.Integration.Tests;

public sealed class OnboardingApiTests(UserManagementDatabase database) : IClassFixture<UserManagementDatabase>
{
    [Fact]
    public async Task Guided_setup_is_atomic_resumable_isolated_and_revalidates_completion()
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("Onboarding", now);
        var unit = Establishment.Create(tenant.Id, "Main", new Slug("setup-" + Guid.NewGuid().ToString("N")), now);
        var otherTenant = Tenant.Create("Other", now);
        var otherUnit = Establishment.Create(otherTenant.Id, "Other", new Slug("other-" + Guid.NewGuid().ToString("N")), now);
        var owner = AdministrativeUser.Create(tenant.Id, "Owner", new Email("owner@test.local"), "hash", AdministrativeRole.Owner, now);
        owner.GrantEstablishmentAccess(unit.Id, tenant.Id, now);
        var admin = AdministrativeUser.Create(tenant.Id, "Admin", new Email("admin@test.local"), "hash", AdministrativeRole.Admin, now);
        var foreign = AdministrativeUser.Create(otherTenant.Id, "Foreign", new Email("foreign@test.local"), "hash", AdministrativeRole.Admin, now);
        await using (var db = new ZFomeDbContext(database.Options)) { db.AddRange(tenant, unit, otherTenant, otherUnit, owner, admin, foreign); await db.SaveChangesAsync(); }
        await using var factory = new Factory(database, owner, unit.Id);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "zf_access=owner; zf_csrf=test");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", "test");
        var root = $"/api/admin/establishments/{unit.Id}";
        var progress = await client.GetFromJsonAsync<OnboardingProgress>(root + "/onboarding");
        Assert.False(progress!.IsReady); Assert.Contains("horarios", progress.PendingSteps); Assert.True(progress.AccessReady);
        await Status(await client.PostAsync(root + "/onboarding/complete", null), HttpStatusCode.Conflict);
        await Status(await client.PutAsJsonAsync(root + "/configuration", new EstablishmentUpdateRequest("Changed", otherUnit.Slug.Value)), HttpStatusCode.Conflict);
        var configurationResponse = await client.GetAsync(root + "/configuration"); Assert.True(configurationResponse.IsSuccessStatusCode, await configurationResponse.Content.ReadAsStringAsync()); var configuration = await configurationResponse.Content.ReadFromJsonAsync<ConfigurationReadModel>();
        Assert.Equal("Main", configuration!.TradeName); Assert.Equal(unit.Slug.Value, configuration.Slug);
        await Status(await client.PutAsJsonAsync(root + "/configuration", new EstablishmentUpdateRequest("Restaurant", unit.Slug.Value)), HttpStatusCode.NoContent);
        await Status(await client.PutAsJsonAsync(root + "/theme", new EstablishmentThemeRequest(PrimaryColor: "#aabbcc")), HttpStatusCode.NoContent);
        configuration = await client.GetFromJsonAsync<ConfigurationReadModel>(root + "/configuration");
        Assert.Equal("#AABBCC", configuration!.Theme.PrimaryColor); Assert.Equal(EstablishmentTheme.DefaultSecondaryColor, configuration.Theme.SecondaryColor);
        await Status(await client.PutAsJsonAsync(root + "/theme", new EstablishmentThemeRequest(LogoUrl: "javascript:alert(1)")), HttpStatusCode.BadRequest);
        var hours = new[] { new BusinessHoursRequest(1, new(9, 0), new(18, 0)) };
        await Status(await client.PutAsJsonAsync(root + "/business-hours", new ReplaceBusinessHoursRequest(hours)), HttpStatusCode.NoContent);
        await Status(await client.PutAsJsonAsync(root + "/business-hours", new ReplaceBusinessHoursRequest([new(2, new(20, 0), new(10, 0)), new(3, new(9, 0), new(18, 0))])), HttpStatusCode.UnprocessableEntity);
        configuration = await client.GetFromJsonAsync<ConfigurationReadModel>(root + "/configuration");
        Assert.Equal(DayOfWeek.Monday, Assert.Single(configuration!.Hours).DayOfWeek);
        await Status(await client.PutAsJsonAsync(root + "/business-hours", new ReplaceBusinessHoursRequest([new(9, new(9, 0), new(18, 0))])), HttpStatusCode.BadRequest);
        var tableRequest = new CreateTableRequest(Guid.NewGuid(), "a1", "Window");
        var retries = await Task.WhenAll(client.PostAsJsonAsync(root + "/tables", tableRequest), client.PostAsJsonAsync(root + "/tables", tableRequest));
        var ids = new List<Guid>();
        foreach (var response in retries) { await Status(response, HttpStatusCode.OK); ids.Add((await response.Content.ReadFromJsonAsync<Created>())!.Id); }
        Assert.Equal(ids[0], ids[1]);
        await Status(await client.PostAsJsonAsync(root + "/tables", tableRequest with { IntentId = Guid.NewGuid() }), HttpStatusCode.Conflict);
        var tables = await client.GetFromJsonAsync<TableSearchResult>(root + "/tables");
        var table = Assert.Single(tables!.Items);
        Assert.Equal("A1", table.Code); Assert.NotNull(table.PublicPath);
        Assert.DoesNotContain(tenant.Id.ToString(), table.PublicPath); Assert.DoesNotContain(unit.Id.ToString(), table.PublicPath); Assert.DoesNotContain(table.Id.ToString(), table.PublicPath);
        var token = table.PublicPath.Split('/').Last();
        var operations = new OperationsReadGateway(new NpgsqlReadConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = database.Database.GetConnectionString() })));
        Assert.NotNull(await operations.ResolveTableAsync(unit.Slug.Value, token, default));
        Assert.Null(await operations.ResolveTableAsync(otherUnit.Slug.Value, token, default));
        await Status(await client.PostAsync(root + $"/tables/{table.Id}/rotate-token", null), HttpStatusCode.NoContent);
        Assert.Null(await operations.ResolveTableAsync(unit.Slug.Value, token, default));
        table = Assert.Single((await client.GetFromJsonAsync<TableSearchResult>(root + "/tables"))!.Items);
        Assert.NotEqual(token, table.PublicPath!.Split('/').Last());
        var newToken = table.PublicPath.Split('/').Last();
        await Status(await client.PutAsJsonAsync(root + $"/tables/{table.Id}", new UpdateTableRequest("B1", "Patio", false)), HttpStatusCode.NoContent);
        Assert.Null(await operations.ResolveTableAsync(unit.Slug.Value, newToken, default));
        Assert.Null(Assert.Single((await client.GetFromJsonAsync<TableSearchResult>(root + "/tables"))!.Items).PublicPath);
        await Status(await client.PutAsJsonAsync(root + $"/tables/{table.Id}", new UpdateTableRequest("B1", "Patio", true)), HttpStatusCode.NoContent);
        Assert.NotNull(await operations.ResolveTableAsync(unit.Slug.Value, newToken, default));
        await Status(await client.GetAsync(root + "/tables?pageSize=101"), HttpStatusCode.BadRequest);
        await Status(await client.PutAsJsonAsync(root + $"/users/{owner.Id}/access", new { granted = false }), HttpStatusCode.Conflict);
        await Status(await client.PutAsJsonAsync(root + $"/users/{foreign.Id}/access", new { granted = true }), HttpStatusCode.NotFound);
        await Status(await client.PutAsJsonAsync(root + $"/users/{admin.Id}/access", new { granted = true }), HttpStatusCode.NoContent);
        await Status(await client.PutAsJsonAsync(root + $"/users/{admin.Id}/roles/999", new { granted = true }), HttpStatusCode.BadRequest);
        await Status(await client.PostAsync(root + "/onboarding/complete", null), HttpStatusCode.NoContent);
        progress = await client.GetFromJsonAsync<OnboardingProgress>(root + "/onboarding");
        Assert.True(progress!.IsReady); Assert.NotNull(progress.CompletedAt);
        var completed = progress.CompletedAt;
        await Status(await client.PostAsync(root + "/onboarding/complete", null), HttpStatusCode.NoContent);
        Assert.Equal(completed, (await client.GetFromJsonAsync<OnboardingProgress>(root + "/onboarding"))!.CompletedAt);
        await Status(await client.PutAsJsonAsync(root + "/business-hours", new ReplaceBusinessHoursRequest([])), HttpStatusCode.NoContent);
        progress = await client.GetFromJsonAsync<OnboardingProgress>(root + "/onboarding");
        Assert.False(progress!.IsReady); Assert.Equal(completed, progress.CompletedAt);
        await Status(await client.PostAsync(root + "/onboarding/complete", null), HttpStatusCode.Conflict);
        foreach (var suffix in new[] { "/onboarding", "/configuration", "/tables" })
            await Status(await client.GetAsync($"/api/admin/establishments/{otherUnit.Id}" + suffix), HttpStatusCode.Forbidden);
        await Status(await client.PutAsJsonAsync($"/api/admin/establishments/{otherUnit.Id}/configuration", new EstablishmentUpdateRequest("Cross", "cross-unit")), HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Remove("Cookie"); client.DefaultRequestHeaders.Add("Cookie", "zf_access=kitchen; zf_csrf=test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(root + "/onboarding")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(root + "/theme", new EstablishmentThemeRequest())).StatusCode);
    }

    [Fact]
    public async Task Removing_last_unit_admin_is_blocked_even_with_another_admin_in_the_tenant()
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("Continuity", now);
        var unit = Establishment.Create(tenant.Id, "One", new Slug("one-" + Guid.NewGuid().ToString("N")), now);
        var second = Establishment.Create(tenant.Id, "Two", new Slug("two-" + Guid.NewGuid().ToString("N")), now);
        var owner = AdministrativeUser.Create(tenant.Id, "Owner", new Email("o@test.local"), "hash", AdministrativeRole.Owner, now);
        var admin = AdministrativeUser.Create(tenant.Id, "Admin", new Email("a@test.local"), "hash", AdministrativeRole.Admin, now);
        owner.GrantEstablishmentAccess(unit.Id, tenant.Id, now); admin.GrantEstablishmentAccess(second.Id, tenant.Id, now);
        await using (var db = new ZFomeDbContext(database.Options)) { db.AddRange(tenant, unit, second, owner, admin); await db.SaveChangesAsync(); }
        await using var factory = new Factory(database, owner, unit.Id);
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("Cookie", "zf_access=owner; zf_csrf=test"); client.DefaultRequestHeaders.Add("X-CSRF-Token", "test");
        var root = $"/api/admin/establishments/{unit.Id}/users";
        await Status(await client.PutAsJsonAsync(root + $"/{owner.Id}/access", new { granted = false }), HttpStatusCode.Conflict);
        await Status(await client.PatchAsJsonAsync(root + $"/{admin.Id}/active", new { isActive = false }), HttpStatusCode.Conflict);
        await Status(await client.PutAsJsonAsync(root + $"/{admin.Id}/roles/2", new { granted = false }), HttpStatusCode.Conflict);
    }
    private static async Task Status(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        if ((int)expected >= 400) Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
    private sealed record Created(Guid Id);
    private sealed class Factory(UserManagementDatabase database, AdministrativeUser owner, Guid unitId) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Database:ConnectionString"] = database.Database.GetConnectionString() }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ZFomeDbContext>(); services.AddScoped(_ => new ZFomeDbContext(database.Options));
                services.RemoveAll<IAuthenticationSessionResolver>(); services.AddSingleton<IAuthenticationSessionResolver>(new Resolver(owner, unitId));
            });
        }
    }
    private sealed class Resolver(AdministrativeUser owner, Guid unitId) : IAuthenticationSessionResolver
    {
        public Task<AuthenticatedIdentity?> ResolveAsync(string token, CancellationToken ct) => Task.FromResult<AuthenticatedIdentity?>(
            token is "owner" or "kitchen" ? new(Guid.NewGuid(), AuthenticationIdentityType.AdministrativeUser, owner.Id, owner.TenantId, [token == "owner" ? AdministrativeRole.Owner : AdministrativeRole.Kitchen], [unitId], false) : null);
    }
}
