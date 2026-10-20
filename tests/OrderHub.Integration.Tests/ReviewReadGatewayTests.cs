using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderHub.Domain.Ordering;
using OrderHub.Domain.Reviews;
using OrderHub.Domain.Tenancy;
using OrderHub.Infrastructure.Persistence;
using OrderHub.Infrastructure.Persistence.Read;
using OrderHub.Infrastructure.Persistence.Write;
using Testcontainers.PostgreSql;

namespace OrderHub.Integration.Tests;

public sealed class ReviewReadGatewayTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Admin_and_public_lists_materialize_provider_types_and_preserve_scope()
    {
        var options = new DbContextOptionsBuilder<OrderHubDbContext>()
            .UseNpgsql(database.GetConnectionString()).Options;
        var createdAt = new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddMinutes(5);

        await using (var context = new OrderHubDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            var tenant = Tenant.Create("Group", createdAt);
            var unit = Establishment.Create(tenant.Id, "Unit", new Slug("unit"), createdAt);
            var otherUnit = Establishment.Create(tenant.Id, "Other", new Slug("other"), createdAt);
            var publishedOrder = CreateOrder(tenant.Id, unit.Id, createdAt);
            var pendingOrder = CreateOrder(tenant.Id, unit.Id, createdAt);
            var otherOrder = CreateOrder(tenant.Id, otherUnit.Id, createdAt);
            var published = Review.Submit(tenant.Id, unit.Id, publishedOrder.Id,
                ReviewTargetKind.Establishment, unit.Id, 5, "Excellent service", createdAt);
            published.Approve(updatedAt);
            var pending = Review.Submit(tenant.Id, unit.Id, pendingOrder.Id,
                ReviewTargetKind.Establishment, unit.Id, 4, null, createdAt.AddMinutes(1));
            var other = Review.Submit(tenant.Id, otherUnit.Id, otherOrder.Id,
                ReviewTargetKind.Establishment, otherUnit.Id, 3, null, createdAt.AddMinutes(2));
            other.Approve(updatedAt);

            context.AddRange(tenant, unit, otherUnit, publishedOrder, pendingOrder, otherOrder,
                published, pending, other);
            await context.SaveChangesAsync();
        }

        var gateway = new ReviewReadGateway(new NpgsqlReadConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = database.GetConnectionString() })));
        await using var verification = new OrderHubDbContext(options);
        var tenantId = await verification.Tenants.Select(tenant => tenant.Id).SingleAsync();
        var unitId = await verification.Establishments.Where(unit => unit.TradeName == "Unit")
            .Select(unit => unit.Id).SingleAsync();

        var admin = await gateway.ListAdminAsync(tenantId, unitId, null, null, null, null,
            1, 20, CancellationToken.None);
        var publicPage = await gateway.ListPublicAsync(tenantId, unitId, null,
            1, 20, CancellationToken.None);

        Assert.Equal(2, admin.Total);
        Assert.Equal([ReviewStatus.Pending, ReviewStatus.Published], admin.Items.Select(item => item.Status));
        var publishedAdmin = Assert.Single(admin.Items, item => item.Status == ReviewStatus.Published);
        Assert.Equal(createdAt, publishedAdmin.CreatedAt);
        Assert.Equal(updatedAt, publishedAdmin.UpdatedAt);
        var publicReview = Assert.Single(publicPage.Items);
        Assert.Equal(1, publicPage.Total);
        Assert.Equal(5, publicReview.Rating);
        Assert.Equal("Excellent service", publicReview.Comment);
        Assert.Equal(createdAt, publicReview.CreatedAt);
    }

    private static Order CreateOrder(Guid tenantId, Guid establishmentId, DateTimeOffset now) =>
        Order.Create(tenantId, establishmentId, OrderServiceType.Pickup,
            null, null, null, null, null, now);
}
