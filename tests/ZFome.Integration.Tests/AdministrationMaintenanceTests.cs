using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZFome.Application.Abstractions.Identity;
using ZFome.Contracts.Administration;
using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence.Write;

namespace ZFome.Integration.Tests;

public sealed class AdministrationMaintenanceTests(UserManagementDatabase database) : IClassFixture<UserManagementDatabase>
{
    [Fact]
    public async Task Existing_maintenance_endpoints_roundtrip_customers_addresses_coupons_and_methods()
    {
        await using var db = new ZFomeDbContext(database.Options);
        var now = DateTimeOffset.UtcNow;
        var tenant = Tenant.Create("Maintenance", now);
        var unit = Establishment.Create(tenant.Id, "Unit", new Slug("maintenance-" + Guid.NewGuid().ToString("N")), now);
        var actor = AdministrativeUser.Create(tenant.Id, "Manager", new Email("manager@example.test"), "hash", AdministrativeRole.Manager, now);
        actor.GrantEstablishmentAccess(unit.Id, tenant.Id, now);
        db.AddRange(tenant, unit, actor); await db.SaveChangesAsync();
        await using var factory = new Factory(database, actor, unit.Id);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "zf_access=manager; zf_csrf=test");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", "test");
        var root = $"/api/admin/establishments/{unit.Id}";
        var customerId = await CreatedId(await client.PostAsJsonAsync(root + "/customers", new CustomerUpsertRequest("Maria", "11999999999", null)));
        var address = new CustomerAddressUpsertRequest("Casa", "Rua A", "10", null, "Centro", "Cidade", "SP", "01001000", true);
        var home = await CreatedId(await client.PostAsJsonAsync(root + $"/customers/{customerId}/addresses", address));
        var work = await CreatedId(await client.PostAsJsonAsync(root + $"/customers/{customerId}/addresses", address with { Label = "Trabalho", IsPrimary = false }));
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(root + $"/customers/{customerId}/addresses/{work}", address with { Label = "Trabalho" })).StatusCode);
        var customers = await client.GetFromJsonAsync<PagedResponse<CustomerResponse>>(root + "/customers?search=Maria&pageSize=1");
        var customer = Assert.Single(customers!.Items);
        Assert.Equal(work, Assert.Single(customer.Addresses, x => x.IsPrimary).Id);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(root + $"/customers/{customerId}/addresses/{home}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(root + "/customers?pageSize=101")).StatusCode);

        var coupon = new CouponUpsertRequest("SAVE10", "Promo", "Percentage", 10, 0, now, now.AddDays(1), null);
        var couponId = await CreatedId(await client.PostAsJsonAsync(root + "/coupons", coupon));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(root + "/coupons", coupon with { Code = "save10" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync(root + $"/coupons/{couponId}/active", new SetActiveRequest(false))).StatusCode);
        var coupons = await client.GetFromJsonAsync<PagedResponse<CouponResponse>>(root + "/coupons?isActive=false&search=save10");
        Assert.Equal(couponId, Assert.Single(coupons!.Items).Id);
        Assert.False(coupons.Items[0].IsActive);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(root + $"/coupons/{couponId}", coupon with { Value = 20 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync(root + $"/coupons/{couponId}/active", new SetActiveRequest(true))).StatusCode);

        var method = new PaymentMethodUpsertRequest("CASH", "Dinheiro", false, true);
        var methodId = await CreatedId(await client.PostAsJsonAsync(root + "/payment-methods", method));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(root + "/payment-methods", method)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(root + $"/payment-methods/{methodId}", method with { Name = "Dinheiro local" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync(root + $"/payment-methods/{methodId}/active", new SetActiveRequest(false))).StatusCode);
        var methods = await client.GetFromJsonAsync<PagedResponse<PaymentMethodResponse>>(root + "/payment-methods?isActive=false&search=CASH");
        Assert.Equal("Dinheiro local", Assert.Single(methods!.Items).Name); Assert.False(methods.Items[0].IsActive);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync(root + $"/payment-methods/{methodId}/active", new SetActiveRequest(true))).StatusCode);
        foreach (var resource in new[] { "customers", "coupons", "payment-methods" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/establishments/{Guid.NewGuid()}/{resource}")).StatusCode);
        client.DefaultRequestHeaders.Remove("Cookie"); client.DefaultRequestHeaders.Add("Cookie", "zf_access=kitchen; zf_csrf=test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(root + "/payment-methods", method)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(root + "/coupons", coupon)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(root + $"/customers/{customerId}", new CustomerUpsertRequest("Denied", "11999999999", null))).StatusCode);
    }

    private static async Task<Guid> CreatedId(HttpResponseMessage response)
    {
        using (response)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, content);
            return JsonDocument.Parse(content).RootElement.GetProperty("id").GetGuid();
        }
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
