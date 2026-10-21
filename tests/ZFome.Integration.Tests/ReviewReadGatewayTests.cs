using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZFome.Domain.Ordering;
using ZFome.Domain.Reviews;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence;
using ZFome.Infrastructure.Persistence.Read;
using ZFome.Infrastructure.Persistence.Write;
using ZFome.Infrastructure.Persistence.Write.Repositories;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class ReviewReadGatewayTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Admin_and_public_lists_materialize_provider_types_and_preserve_scope()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>()
            .UseNpgsql(database.GetConnectionString()).Options;
        var createdAt = new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddMinutes(5);

        await using (var context = new ZFomeDbContext(options))
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
        await using var verification = new ZFomeDbContext(options);
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

    [Fact]
    public async Task Removing_a_published_rating_updates_summary_without_negative_insert()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>()
            .UseNpgsql(database.GetConnectionString()).Options;
        var now = new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero);
        var tenantId = Guid.NewGuid();
        var establishmentId = Guid.NewGuid();

        await using (var context = new ZFomeDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            var tenant = Tenant.Create("Group", now);
            var establishment = Establishment.Create(tenant.Id, "Unit", new Slug("unit"), now);
            tenantId = tenant.Id;
            establishmentId = establishment.Id;
            context.AddRange(tenant, establishment);
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                insert into reviews.rating_summary
                    (tenant_id, establishment_id, target_kind, target_id, count, rating_sum,
                     rating_1_count, rating_2_count, rating_3_count, rating_4_count, rating_5_count, updated_at)
                values ({tenantId}, {establishmentId}, {"Establishment"}, {establishmentId}, 1, 5, 0, 0, 0, 0, 1, {now})
                """);
        }

        await using (var context = new ZFomeDbContext(options))
        {
            var repository = new ReviewRepository(context);
            await repository.ApplyRatingDeltaAsync(tenantId, establishmentId, ReviewTargetKind.Establishment,
                establishmentId, 5, -1, now.AddMinutes(1), CancellationToken.None);
            var values = await context.Database.SqlQuery<SummaryCounters>($"""
                select count as "Count", rating_sum as "RatingSum", rating_5_count as "Rating5Count"
                from reviews.rating_summary
                where tenant_id = {tenantId} and establishment_id = {establishmentId}
                """).SingleAsync();
            Assert.Equal(0, values.Count);
            Assert.Equal(0, values.RatingSum);
            Assert.Equal(0, values.Rating5Count);
        }
    }

    private sealed record SummaryCounters(long Count, long RatingSum, long Rating5Count);

    private static Order CreateOrder(Guid tenantId, Guid establishmentId, DateTimeOffset now) =>
        Order.Create(tenantId, establishmentId, OrderServiceType.Pickup,
            null, null, null, null, null, now);
}
