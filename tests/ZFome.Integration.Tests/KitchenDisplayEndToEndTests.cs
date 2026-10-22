using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Contracts.Administration;
using ZFome.Domain.Identity;
using ZFome.Domain.Ordering;
using ZFome.Domain.SharedKernel;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence;
using ZFome.Infrastructure.Persistence.Write;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class KitchenDisplayEndToEndTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Confirmed_order_moves_through_kitchen_queue_until_ready()
    {
        var now = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        var tenant = Tenant.Create("Kitchen tenant", now);
        var establishment = Establishment.Create(
            tenant.Id,
            "Kitchen unit",
            new Slug("kitchen-e2e"),
            now);
        var order = Order.Create(
            tenant.Id,
            establishment.Id,
            OrderServiceType.Pickup,
            null,
            "Maria",
            null,
            null,
            null,
            now);
        order.AddItem(
            Guid.NewGuid(),
            null,
            "Pizza",
            "Grande",
            new Money(45),
            new Quantity(1),
            [new(Guid.NewGuid(), "Sem azeitona", Money.Zero, new Quantity(1))],
            "Cortar em oito pedaços",
            now);
        order.Confirm(17, now);

        var options = new DbContextOptionsBuilder<ZFomeDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options;
        await using (var setup = new ZFomeDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.AddRange(tenant, establishment, order);
            await setup.SaveChangesAsync();
        }

        await using var factory = new Factory(database.GetConnectionString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Tenant", tenant.Id.ToString());
        client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", AdministrativeRole.Kitchen.ToString());
        var root = $"/api/admin/establishments/{establishment.Id}";

        var confirmedQueue = await client.GetFromJsonAsync<KitchenTicketResponse[]>(root + "/kitchen");
        var confirmed = Assert.Single(confirmedQueue!);
        Assert.Equal("Confirmed", confirmed.Status);
        Assert.Equal("StartPreparation", confirmed.Action);
        Assert.Equal("Cortar em oito pedaços", Assert.Single(confirmed.Items).Notes);

        using var prepare = await client.PostAsJsonAsync(
            root + $"/orders/{order.Id}/prepare",
            new OrderTransitionRequest(null));
        Assert.Equal(HttpStatusCode.NoContent, prepare.StatusCode);

        var preparingQueue = await client.GetFromJsonAsync<KitchenTicketResponse[]>(root + "/kitchen");
        var preparing = Assert.Single(preparingQueue!);
        Assert.Equal("Preparing", preparing.Status);
        Assert.Equal("MarkReady", preparing.Action);
        Assert.NotNull(preparing.PreparationStartedAt);

        using var ready = await client.PostAsJsonAsync(
            root + $"/orders/{order.Id}/ready",
            new OrderTransitionRequest(null));
        Assert.Equal(HttpStatusCode.NoContent, ready.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<KitchenTicketResponse[]>(root + "/kitchen"))!);

        await using var verify = new ZFomeDbContext(options);
        var persisted = await verify.Orders
            .Include(value => value.History)
            .SingleAsync(value => value.Id == order.Id);
        Assert.Equal(OrderStatus.Ready, persisted.Status);
        Assert.Contains(persisted.History, entry => entry.NewStatus == OrderStatus.Preparing);
        Assert.Contains(persisted.History, entry => entry.NewStatus == OrderStatus.Ready);
    }

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Database:ConnectionString", connectionString);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });
                services.RemoveAll<IEstablishmentAccessGateway>();
                services.AddSingleton<IEstablishmentAccessGateway, AllowAccessGateway>();
            });
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "KitchenE2E";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var tenant = Request.Headers["X-Test-Tenant"].ToString();
            var user = Request.Headers["X-Test-User"].ToString();
            var role = Request.Headers["X-Test-Role"].ToString();
            var identity = new ClaimsIdentity([
                new Claim("tenant_id", tenant),
                new Claim("sub", user),
                new Claim(ClaimTypes.Role, role)
            ], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class AllowAccessGateway : IEstablishmentAccessGateway
    {
        public Task<bool> HasActiveAccessAsync(
            Guid tenantId,
            Guid userId,
            Guid establishmentId,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
