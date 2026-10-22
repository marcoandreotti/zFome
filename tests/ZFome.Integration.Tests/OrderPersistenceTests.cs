using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZFome.Domain.Ordering;
using ZFome.Domain.Catalog;
using ZFome.Domain.SharedKernel;
using ZFome.Domain.Tenancy;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Infrastructure.Persistence;
using ZFome.Infrastructure.Persistence.Read;
using ZFome.Infrastructure.Persistence.Write;
using Testcontainers.PostgreSql;

namespace ZFome.Integration.Tests;

public sealed class OrderPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task Ef_and_Dapper_preserve_order_snapshots_totals_and_history()
    {
        var options = CreateOptions();
        Guid tenantId; Guid establishmentId; Guid orderId;
        await using (var context = new ZFomeDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            var now = DateTimeOffset.UtcNow; var tenant = Tenant.Create("Group", now); var unit = Establishment.Create(tenant.Id, "Unit", new Slug("unit"), now);
            var order = Order.Create(tenant.Id, unit.Id, OrderServiceType.Delivery, null, "Maria", "11999998888", null, new("Rua A", "10", null, "Centro", "São Paulo", "SP", "01001000"), now);
            order.AddItem(Guid.NewGuid(), null, "Produto antigo", null, new Money(12.50m), new Quantity(2), [new(Guid.NewGuid(), "Adicional antigo", new Money(1.25m), new Quantity(1))], "observação", now);
            order.Confirm(1, now); order.StartPreparation(now.AddMinutes(1), Guid.NewGuid());
            context.AddRange(tenant, unit, order); await context.SaveChangesAsync();
            tenantId = tenant.Id; establishmentId = unit.Id; orderId = order.Id;
        }

        var gateway = new OrderReadGateway(new NpgsqlReadConnectionFactory(Microsoft.Extensions.Options.Options.Create(new DatabaseOptions { ConnectionString = database.GetConnectionString() })), TimeProvider.System);
        var result = await gateway.GetAsync(tenantId, establishmentId, orderId, CancellationToken.None);

        Assert.NotNull(result); Assert.Equal("Produto antigo", Assert.Single(result.Items).ProductName); Assert.Equal("Adicional antigo", Assert.Single(result.Items[0].Additionals).Name);
        Assert.Equal(27.50m, result.Total); Assert.Equal(0m, result.ConfirmedAmount); Assert.False(result.IsFullyPaid);
        Assert.Equal(2, result.History.Count); Assert.Equal("Rua A", result.DeliveryAddress!.Street);
        Assert.Null(await gateway.GetAsync(Guid.NewGuid(), establishmentId, orderId, CancellationToken.None));
        var filtered=await gateway.SearchAsync(tenantId,establishmentId,null,null,OrderStatus.Preparing,1,OrderServiceType.Delivery,1,20,CancellationToken.None);
        Assert.Equal(1,filtered.Total);var summary=Assert.Single(filtered.Items);Assert.Equal(orderId,summary.Id);
        var summaryItem=Assert.Single(summary.Items!);Assert.Equal(2,summaryItem.Quantity);Assert.Equal("Produto antigo",summaryItem.ProductName);Assert.Null(summaryItem.VariationName);
        Assert.Empty((await gateway.SearchAsync(Guid.NewGuid(),establishmentId,null,null,null,null,null,1,20,CancellationToken.None)).Items);
        Assert.Empty((await gateway.SearchAsync(tenantId,establishmentId,null,null,OrderStatus.Completed,null,null,1,20,CancellationToken.None)).Items);
    }

    [Fact]
    public async Task Ef_and_Dapper_preserve_composed_modifier_price_explanation()
    {
        var options = CreateOptions();
        Guid tenantId; Guid establishmentId; Guid orderId;
        var now = DateTimeOffset.UtcNow;
        var firstOption = Guid.NewGuid(); var secondOption = Guid.NewGuid(); var groupId = Guid.NewGuid();
        await using (var context = new ZFomeDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            var tenant = Tenant.Create("Group", now); var unit = Establishment.Create(tenant.Id, "Unit", new Slug("modifier-unit"), now);
            var composition = new ModifierCompositionPrice(new Money(30), new Money(50), [
                new ModifierGroupSnapshot(groupId, "Sabores", ModifierPricingStrategy.HighestPrice, new Money(50), [
                    new(groupId, firstOption, "Calabresa", new Money(40), new Quantity(1), new ModifierPortion(1, 2)),
                    new(groupId, secondOption, "Portuguesa", new Money(50), new Quantity(1), new ModifierPortion(1, 2))])]);
            var order = Order.Create(tenant.Id, unit.Id, OrderServiceType.Pickup, null, null, null, null, null, now);
            order.AddComposedItem(Guid.NewGuid(), null, "Pizza", "Grande", composition, new Quantity(2), null, now);
            order.Confirm(1, now); context.AddRange(tenant, unit, order); await context.SaveChangesAsync();
            tenantId = tenant.Id; establishmentId = unit.Id; orderId = order.Id;
        }

        var gateway = new OrderReadGateway(new NpgsqlReadConnectionFactory(Microsoft.Extensions.Options.Options.Create(new DatabaseOptions { ConnectionString = database.GetConnectionString() })), TimeProvider.System);
        var result = await gateway.GetAsync(tenantId, establishmentId, orderId, CancellationToken.None);
        Assert.NotNull(result);
        var item = Assert.Single(result.Items);
        var group = Assert.Single(item.ModifierGroups!);
        Assert.Equal(30m, item.BasePrice); Assert.Equal(50m, item.UnitPrice); Assert.Equal(100m, item.Total);
        Assert.Equal("HighestPrice", group.PricingStrategy); Assert.Equal(50m, group.Price);
        Assert.Equal([1, 1], group.Options.Select(x => x.PortionNumerator));
        Assert.Equal([2, 2], group.Options.Select(x => x.PortionDenominator));
        Assert.Equal(new[] { firstOption, secondOption }.Order(), group.Options.Select(x => x.OptionId).Order());
    }

    [Fact]
    public async Task Offer_resolver_prices_from_tenant_catalog_and_rejects_foreign_scope_or_group()
    {
        var options = CreateOptions(); var now = DateTimeOffset.UtcNow;
        Guid tenantId; Guid establishmentId; Guid productId; Guid groupId; Guid firstOptionId; Guid secondOptionId;
        await using (var context = new ZFomeDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            var tenant = Tenant.Create("Group", now); var unit = Establishment.Create(tenant.Id, "Unit", new Slug("resolver-unit"), now);
            var category = Category.Create(tenant.Id, unit.Id, "Pizzas"); var product = Product.Create(tenant.Id, unit.Id, category, "P1", "Pizza", new Money(30));
            var first = Additional.Create(tenant.Id, unit.Id, "Calabresa", new Money(40)); var second = Additional.Create(tenant.Id, unit.Id, "Portuguesa", new Money(50));
            var group = AdditionalGroup.Create(tenant.Id, unit.Id, "Sabores", 1, 4, ModifierPricingStrategy.Proportional, ModifierGroupType.Flavor, true);
            group.AddItem(first, 0); group.AddItem(second, 1); product.LinkAdditionalGroup(group, 0);
            context.AddRange(tenant, unit, category, product, first, second, group); await context.SaveChangesAsync();
            tenantId = tenant.Id; establishmentId = unit.Id; productId = product.Id; groupId = group.Id; firstOptionId = first.Id; secondOptionId = second.Id;
        }

        var resolver = new OrderOfferResolver(new NpgsqlReadConnectionFactory(Microsoft.Extensions.Options.Options.Create(new DatabaseOptions { ConnectionString = database.GetConnectionString() })));
        var selections = new[]
        {
            new OrderAdditionalSelection(firstOptionId, 1, groupId, 1, 2),
            new OrderAdditionalSelection(secondOptionId, 1, groupId, 1, 2)
        };
        var offer = await resolver.ResolveAsync(tenantId, establishmentId, productId, null, selections, now, CancellationToken.None);
        Assert.NotNull(offer); Assert.Equal(45m, offer.UnitPrice.Amount); Assert.Equal(30m, offer.Composition!.BasePrice.Amount);
        Assert.Null(await resolver.ResolveAsync(Guid.NewGuid(), establishmentId, productId, null, selections, now, CancellationToken.None));
        Assert.Null(await resolver.ResolveAsync(tenantId, establishmentId, productId, null, [selections[0] with { GroupId = Guid.NewGuid() }, selections[1]], now, CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_sequence_reservations_are_distinct_and_monotonic_per_establishment()
    {
        var options = CreateOptions(); Guid tenantId; Guid establishmentId;
        await using (var setup = new ZFomeDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync(); var now = DateTimeOffset.UtcNow; var tenant = Tenant.Create("Group", now); var unit = Establishment.Create(tenant.Id, "Unit", new Slug("unit"), now); setup.AddRange(tenant, unit); await setup.SaveChangesAsync(); tenantId = tenant.Id; establishmentId = unit.Id;
        }

        var reservations = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var context = new ZFomeDbContext(options); await using var transaction = await context.Database.BeginTransactionAsync();
            var number = await new OrderNumberSequence(context).ReserveAsync(tenantId, establishmentId, CancellationToken.None); await transaction.CommitAsync(); return number;
        });
        var numbers = await Task.WhenAll(reservations);

        Assert.Equal(Enumerable.Range(1, 8).Select(x => (long)x), numbers.Order());
    }

    [Fact]
    public async Task Kitchen_queue_is_ordered_and_isolated_with_complete_preparation_details()
    {
        var options = CreateOptions();
        Guid tenantId;
        Guid firstEstablishmentId;
        Guid preparingOrderId;
        Guid confirmedOrderId;
        var now = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

        await using (var context = new ZFomeDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            var tenant = Tenant.Create("Group", now);
            var first = Establishment.Create(tenant.Id, "First", new Slug("kitchen-first"), now);
            var second = Establishment.Create(tenant.Id, "Second", new Slug("kitchen-second"), now);
            var preparing = CreateKitchenOrder(tenant.Id, first.Id, 1, now, "Sem cebola");
            preparing.StartPreparation(now.AddMinutes(3), Guid.NewGuid());
            var confirmed = CreateKitchenOrder(tenant.Id, first.Id, 2, now.AddMinutes(1), null);
            var otherUnit = CreateKitchenOrder(tenant.Id, second.Id, 1, now, null);
            var completed = CreateKitchenOrder(tenant.Id, first.Id, 3, now.AddMinutes(2), null);
            completed.StartPreparation(now.AddMinutes(3), Guid.NewGuid());
            completed.MarkReady(now.AddMinutes(4), Guid.NewGuid());
            context.AddRange(tenant, first, second, preparing, confirmed, otherUnit, completed);
            await context.SaveChangesAsync();
            tenantId = tenant.Id;
            firstEstablishmentId = first.Id;
            preparingOrderId = preparing.Id;
            confirmedOrderId = confirmed.Id;
        }

        var gateway = new KitchenDisplayReadGateway(new NpgsqlReadConnectionFactory(
            Microsoft.Extensions.Options.Options.Create(new DatabaseOptions
            {
                ConnectionString = database.GetConnectionString()
            })), TimeProvider.System);

        var queue = await gateway.GetQueueAsync(
            tenantId,
            firstEstablishmentId,
            CancellationToken.None);

        Assert.Equal([preparingOrderId, confirmedOrderId], queue.Select(ticket => ticket.Id));
        var firstTicket = queue[0];
        Assert.Equal(OrderStatus.Preparing, firstTicket.Status);
        Assert.NotNull(firstTicket.PreparationStartedAt);
        var item = Assert.Single(firstTicket.Items);
        Assert.Equal("Sem cebola", item.Notes);
        Assert.Equal(2m, item.Quantity);
        Assert.Equal("Molho", Assert.Single(item.Additionals).Name);
        Assert.Empty(await gateway.GetQueueAsync(
            Guid.NewGuid(),
            firstEstablishmentId,
            CancellationToken.None));
    }

    private static Order CreateKitchenOrder(
        Guid tenantId,
        Guid establishmentId,
        long number,
        DateTimeOffset now,
        string? notes)
    {
        var order = Order.Create(
            tenantId,
            establishmentId,
            OrderServiceType.Pickup,
            null,
            "Cliente",
            null,
            null,
            null,
            now);
        order.AddItem(
            Guid.NewGuid(),
            null,
            "Hambúrguer",
            "Duplo",
            new Money(20),
            new Quantity(2),
            [new(Guid.NewGuid(), "Molho", new Money(1), new Quantity(1))],
            notes,
            now);
        order.Confirm(number, now);
        return order;
    }

    private DbContextOptions<ZFomeDbContext> CreateOptions() => new DbContextOptionsBuilder<ZFomeDbContext>().UseNpgsql(database.GetConnectionString()).Options;
}
