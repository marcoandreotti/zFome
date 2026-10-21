using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZFome.Domain.Operations;
using ZFome.Domain.Catalog;
using ZFome.Domain.Ordering;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence;
using ZFome.Infrastructure.Persistence.Read;
using ZFome.Infrastructure.Persistence.Write;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class OperationsPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Qr_requires_matching_slug_and_token_and_hours_are_unit_scoped()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var context = new ZFomeDbContext(options); await context.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow; var tenant = Tenant.Create("Tenant", now);
        var first = Establishment.Create(tenant.Id, "First", new Slug("ops-first"), now);
        var second = Establishment.Create(tenant.Id, "Second", new Slug("ops-second"), now);
        var table = ServiceTable.Create(tenant.Id, first.Id, "A1");
        var hours = BusinessHours.Create(tenant.Id, first.Id, DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(20, 0));
        context.AddRange(tenant, first, second, table, hours); await context.SaveChangesAsync();
        var gateway = new OperationsReadGateway(new NpgsqlReadConnectionFactory(Options.Create(new DatabaseOptions { ConnectionString = database.GetConnectionString() })));
        Assert.NotNull(await gateway.ResolveTableAsync("ops-first", table.QrCodeToken, CancellationToken.None));
        Assert.Null(await gateway.ResolveTableAsync("ops-second", table.QrCodeToken, CancellationToken.None));
        Assert.True(await gateway.IsOpenAsync(tenant.Id, first.Id, DayOfWeek.Monday, new TimeOnly(12, 0), CancellationToken.None));
        Assert.False(await gateway.IsOpenAsync(tenant.Id, second.Id, DayOfWeek.Monday, new TimeOnly(12, 0), CancellationToken.None));
    }

    [Fact]
    public async Task Availability_records_are_tenant_and_establishment_scoped()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var context = new ZFomeDbContext(options); await context.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow; var tenant = Tenant.Create("Tenant", now);
        var first = Establishment.Create(tenant.Id, "First", new Slug("availability-first"), now);
        var second = Establishment.Create(tenant.Id, "Second", new Slug("availability-second"), now);
        var exception = ServiceScheduleException.CreateClosed(tenant.Id, first.Id, DateOnly.FromDateTime(now.UtcDateTime), OrderServiceType.Pickup, "Holiday");
        var pause = ServicePause.Create(tenant.Id, first.Id, OrderServiceType.Delivery, now, now.AddHours(1), "Capacity");
        var unavailable = OfferUnavailability.Create(tenant.Id, first.Id, OfferKind.Product, Guid.NewGuid(), now, null, "Sold out");
        context.AddRange(tenant, first, second, exception, pause, unavailable); await context.SaveChangesAsync();

        var availability = new AvailabilityRepository(context);
        Assert.Single(await availability.GetExceptionsAsync(tenant.Id, first.Id, CancellationToken.None));
        Assert.Empty(await availability.GetExceptionsAsync(tenant.Id, second.Id, CancellationToken.None));
        Assert.NotNull(await availability.GetActivePauseAsync(tenant.Id, first.Id, OrderServiceType.Delivery, now.AddMinutes(1), CancellationToken.None));
        Assert.Equal(first.Id, (await context.OfferUnavailabilities.SingleAsync()).EstablishmentId);
    }
}
