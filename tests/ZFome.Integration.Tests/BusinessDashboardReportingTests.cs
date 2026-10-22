using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Dapper;
using Npgsql;
using ZFome.Domain.Ordering;
using ZFome.Domain.Payments;
using ZFome.Domain.SharedKernel;
using ZFome.Domain.Tenancy;
using ZFome.Infrastructure.Persistence;
using ZFome.Infrastructure.Persistence.Read;
using ZFome.Infrastructure.Persistence.Write;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class BusinessDashboardReportingTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => database.StartAsync();

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Aggregates_completed_sales_confirmed_payments_and_cancellations_in_unit_local_dates()
    {
        var options = new DbContextOptionsBuilder<ZFomeDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options;
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tenant = Tenant.Create("Grupo", now);
        var unit = Establishment.Create(tenant.Id, "Unidade", new Slug("unidade"), now);
        unit.ChangeTimeZone("America/Sao_Paulo", now);
        var paymentMethod = PaymentMethod.Create(tenant.Id, unit.Id, "PIX", "Pix", true, false, now);

        var first = CreateOrder(tenant.Id, unit.Id, 1, "Produto A", 50m, 2m,
            new DateTimeOffset(2026, 1, 10, 0, 45, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 10, 2, 30, 0, TimeSpan.Zero));
        var second = CreateOrder(tenant.Id, unit.Id, 2, "Produto B", 10m, 3m,
            new DateTimeOffset(2026, 1, 10, 13, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 10, 14, 0, 0, TimeSpan.Zero));
        var cancelled = Order.Create(tenant.Id, unit.Id, OrderServiceType.Pickup, null, null, null, null, null, now);
        cancelled.AddItem(Guid.NewGuid(), null, "Produto cancelado", null, new Money(999m), new Quantity(10m), [], null, now);
        cancelled.Confirm(3, new DateTimeOffset(2026, 1, 10, 14, 30, 0, TimeSpan.Zero));
        cancelled.Cancel(new DateTimeOffset(2026, 1, 10, 15, 0, 0, TimeSpan.Zero), null);

        var paidSale = Payment.Create(tenant.Id, unit.Id, first.Id, paymentMethod, new Money(80m), null, now);
        paidSale.Confirm(null, new DateTimeOffset(2026, 1, 10, 17, 0, 0, TimeSpan.Zero));
        var paidCancellation = Payment.Create(tenant.Id, unit.Id, cancelled.Id, paymentMethod, new Money(20m), null, now);
        paidCancellation.Confirm(null, new DateTimeOffset(2026, 1, 10, 18, 0, 0, TimeSpan.Zero));
        var pending = Payment.Create(tenant.Id, unit.Id, second.Id, paymentMethod, new Money(300m), null, now);

        await using (var context = new ZFomeDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.AddRange(tenant, unit, paymentMethod, first, second, cancelled, paidSale, paidCancellation, pending);
            await context.SaveChangesAsync();
        }

        var gateway = new BusinessDashboardReadGateway(new NpgsqlReadConnectionFactory(
            Options.Create(new DatabaseOptions { ConnectionString = database.GetConnectionString() })));
        var result = await gateway.GetAsync(
            tenant.Id, unit.Id,
            new DateOnly(2026, 1, 9), new DateOnly(2026, 1, 10),
            serviceType: null, productPage: 1, productPageSize: 1, CancellationToken.None);

        Assert.Equal("America/Sao_Paulo", result.TimeZoneId);
        Assert.Equal(130m, result.Current.Revenue);
        Assert.Equal(3, result.Current.ReceivedOrders);
        Assert.Equal(2, result.Current.CompletedOrders);
        Assert.Equal(65m, result.Current.AverageTicket);
        Assert.Equal(100m, result.Current.ConfirmedPayments);
        Assert.Equal(1, result.Current.CancelledOrders);
        Assert.Equal(0, result.Current.RejectedOrders);
        Assert.Equal(4, result.Series.Count);
        Assert.Contains(result.Series, x => !x.IsComparison && x.Date == new DateOnly(2026, 1, 9) && x.Revenue == 100m);
        Assert.Contains(result.Series, x => !x.IsComparison && x.Date == new DateOnly(2026, 1, 10) && x.Revenue == 30m);
        Assert.Equal(2, result.Products.Total);
        Assert.Equal("Produto B", Assert.Single(result.Products.Items).Name);
        Assert.Equal(3m, result.Products.Items[0].QuantitySold);

        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync();
        await connection.ExecuteAsync("analyze orders.order_status_history; analyze payments.payment;");
        await connection.ExecuteAsync("set enable_seqscan = off;");
        var historyPlan = await connection.QueryAsync<string>(
            "explain select order_id from orders.order_status_history where tenant_id = @TenantId and establishment_id = @EstablishmentId and new_status = 'Completed' and occurred_at >= @From and occurred_at < @To;",
            new { TenantId = tenant.Id, EstablishmentId = unit.Id, From = DateTimeOffset.UtcNow.AddDays(-1), To = DateTimeOffset.UtcNow });
        var paymentPlan = await connection.QueryAsync<string>(
            "explain select order_id, amount from payments.payment where tenant_id = @TenantId and establishment_id = @EstablishmentId and status = 'Confirmed' and confirmed_at >= @From and confirmed_at < @To;",
            new { TenantId = tenant.Id, EstablishmentId = unit.Id, From = DateTimeOffset.UtcNow.AddDays(-1), To = DateTimeOffset.UtcNow });
        Assert.Contains("ix_order_status_history_reporting", string.Join('\n', historyPlan));
        Assert.Contains("ix_payment_reporting", string.Join('\n', paymentPlan));
    }

    private static Order CreateOrder(
        Guid tenantId,
        Guid unitId,
        long number,
        string productName,
        decimal unitPrice,
        decimal quantity,
        DateTimeOffset confirmedAt,
        DateTimeOffset completedAt)
    {
        var order = Order.Create(tenantId, unitId, OrderServiceType.Pickup, null, null, null, null, null, confirmedAt);
        order.AddItem(Guid.NewGuid(), null, productName, null,
            new Money(unitPrice), new Quantity(quantity), [], null, confirmedAt);
        order.Confirm(number, confirmedAt);
        order.StartPreparation(confirmedAt.AddMinutes(1), Guid.NewGuid());
        order.MarkReady(completedAt.AddMinutes(-1), Guid.NewGuid());
        order.Complete(completedAt, Guid.NewGuid());
        return order;
    }
}
