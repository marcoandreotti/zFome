using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Communications;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Exceptions;
using ZFome.Application.Ordering;
using ZFome.Application.Tenancy;
using ZFome.Domain.Ordering;
using ZFome.Domain.SharedKernel;
using ZFome.Application.Abstractions.Promotions;
using ZFome.Domain.Promotions;
using ZFome.Domain.Operations;

namespace ZFome.Application.Tests.Ordering;

public sealed class OrderApplicationTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid EstablishmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task Draft_resolves_customer_and_table_inside_authenticated_scope()
    {
        var repository = new Repository(); var customer = new CustomerResolver(); var table = new TableResolver();
        var handler = new CreateOrderDraftCommandHandler(Resolver(), repository, customer, table, new Clock());

        var id = await handler.HandleAsync(new(EstablishmentId, OrderServiceType.Table, CustomerResolver.CustomerId, null, TableResolver.TableId, null), CancellationToken.None);

        Assert.Equal(id, repository.Value!.Id); Assert.Equal(TenantId, customer.TenantId); Assert.Equal(EstablishmentId, table.EstablishmentId);
        Assert.Equal("Maria", repository.Value.CustomerName);
    }

    [Fact]
    public async Task Item_uses_offer_snapshot_and_propagates_cancellation_token()
    {
        var order = Draft(); var repository = new Repository(order); var offers = new OfferResolver();
        var handler = new AddOrderItemCommandHandler(Resolver(), repository, offers, new Clock());
        using var source = new CancellationTokenSource();

        await handler.HandleAsync(new(EstablishmentId, order.Id, OfferResolver.ProductId, null, 2, [], "sem cebola"), source.Token);

        Assert.Equal(source.Token, offers.Token); Assert.Equal(20m, order.Total.Amount); Assert.Equal("Produto", Assert.Single(order.Items).ProductName);
    }

    [Fact]
    public async Task Confirmation_revalidates_offer_and_reserves_number_inside_transaction()
    {
        var order = DraftWithItem(); var repository = new Repository(order); var transaction = new Transaction();
        var publisher = new Publisher();
        var handler = new ConfirmOrderCommandHandler(Resolver(), repository, new OfferResolver(), new Sequence(), transaction, new CouponRepository(), new AvailableGateway(), publisher, new Clock());

        await handler.HandleAsync(new(EstablishmentId, order.Id), CancellationToken.None);

        Assert.True(transaction.Executed); Assert.Equal(7, order.Number); Assert.True(repository.Saved); Assert.Equal(UserId, order.History.Last().ActorId);
        Assert.Equal(order.Id, Assert.Single(publisher.Signals).OrderId);
    }

    [Fact]
    public async Task Confirmation_rejects_offer_changed_before_reserving_number()
    {
        var order = DraftWithItem(); var sequence = new Sequence();
        var publisher = new Publisher();
        var handler = new ConfirmOrderCommandHandler(Resolver(), new Repository(order), new OfferResolver(new Money(11)), sequence, new Transaction(), new CouponRepository(), new AvailableGateway(), publisher, new Clock());

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(new(EstablishmentId, order.Id), CancellationToken.None));
        Assert.False(sequence.Called); Assert.Equal(OrderStatus.Draft, order.Status); Assert.Empty(publisher.Signals);
    }

    [Fact]
    public async Task Confirmation_does_not_publish_when_transaction_rolls_back()
    {
        var order = DraftWithItem(); var publisher = new Publisher();
        var handler = new ConfirmOrderCommandHandler(Resolver(), new Repository(order), new OfferResolver(), new Sequence(), new Transaction(failAfterOperation: true), new CouponRepository(), new AvailableGateway(), publisher, new Clock());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new(EstablishmentId, order.Id), CancellationToken.None));

        Assert.Empty(publisher.Signals);
    }

    [Fact]
    public async Task Administrative_transition_records_authenticated_actor()
    {
        var order=DraftWithItem();order.Confirm(1,Clock.Now);var repository=new Repository(order);
        var publisher = new Publisher();
        var handler=new TransitionOrderCommandHandler(Resolver(),repository,new CustomerNotificationStager(),publisher,new Clock());
        await handler.HandleAsync(new(EstablishmentId,order.Id,OrderStatus.Preparing),CancellationToken.None);
        Assert.Equal(UserId,order.History.Last().ActorId);Assert.True(repository.Saved);
        Assert.Equal(OrderUpdateKind.StatusChanged, Assert.Single(publisher.Signals).ChangeType);
    }

    [Fact]
    public async Task Stale_operational_transition_is_reported_as_conflict()
    {
        var order = DraftWithItem();
        order.Confirm(1, Clock.Now);
        order.StartPreparation(Clock.Now.AddMinutes(1), UserId);
        var handler = new TransitionOrderCommandHandler(
            Resolver(),
            new Repository(order),
            new CustomerNotificationStager(),
            new Publisher(),
            new Clock());

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            new TransitionOrderCommand(EstablishmentId, order.Id, OrderStatus.Preparing),
            CancellationToken.None));
    }

    [Fact]
    public async Task Query_uses_authenticated_tenant_scope()
    {
        var gateway = new ReadGateway(); var handler = new GetOrderQueryHandler(Resolver(), gateway);
        await handler.HandleAsync(new(EstablishmentId, Guid.NewGuid()), CancellationToken.None);
        Assert.Equal(TenantId, gateway.TenantId); Assert.Equal(EstablishmentId, gateway.EstablishmentId);
    }

    [Fact]
    public async Task Validators_reject_invalid_composition_and_transition()
    {
        Assert.False((await new CreateOrderDraftCommandValidator().ValidateAsync(new CreateOrderDraftCommand(Guid.Empty, OrderServiceType.Table, null, null, null, null))).IsValid);
        Assert.False((await new AddOrderItemCommandValidator().ValidateAsync(new AddOrderItemCommand(Guid.Empty, Guid.Empty, Guid.Empty, null, 0, [], null))).IsValid);
        Assert.False((await new ConfirmOrderCommandValidator().ValidateAsync(new ConfirmOrderCommand(Guid.Empty, Guid.Empty))).IsValid);
        Assert.False((await new SearchOrdersQueryValidator().ValidateAsync(new SearchOrdersQuery(EstablishmentId,null,null,null,null,null,1,101))).IsValid);
    }

    private static Order Draft() => Order.Create(TenantId, EstablishmentId, OrderServiceType.Pickup, null, null, null, null, null, Clock.Now);
    private static Order DraftWithItem() { var order = Draft(); order.AddItem(OfferResolver.ProductId, null, "Produto", null, new Money(10), new Quantity(1), [], null, Clock.Now); return order; }
    private static EstablishmentScopeResolver Resolver() => new(new TenantContext(), new AccessGateway());

    private sealed class TenantContext : ITenantContext
    { public bool HasTenant => true; public Guid TenantId => OrderApplicationTests.TenantId; public bool HasUser => true; public Guid UserId => OrderApplicationTests.UserId; public Guid GetRequiredTenantId() => TenantId; public Guid GetRequiredUserId() => UserId; }
    private sealed class AccessGateway : IEstablishmentAccessGateway
    { public Task<bool> HasActiveAccessAsync(Guid tenantId, Guid userId, Guid establishmentId, CancellationToken cancellationToken) => Task.FromResult(tenantId == TenantId && userId == UserId && establishmentId == EstablishmentId); }
    private sealed class Clock : TimeProvider { public static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Repository(Order? value = null) : IOrderRepository
    { public Order? Value { get; private set; } = value; public bool Saved { get; private set; } public Task<Order?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) => Task.FromResult(Value is { } order && order.TenantId == tenantId && order.EstablishmentId == establishmentId && order.Id == id ? order : null); public Task AddAsync(Order order, CancellationToken cancellationToken) { Value = order; return Task.CompletedTask; } public Task SaveChangesAsync(CancellationToken cancellationToken) { Saved = true; return Task.CompletedTask; } }
    private sealed class OfferResolver(Money? price = null) : IOrderOfferResolver
    { public static readonly Guid ProductId = Guid.Parse("44444444-4444-4444-4444-444444444444"); public CancellationToken Token { get; private set; } public Task<OrderOfferSnapshot?> ResolveAsync(Guid tenantId, Guid establishmentId, Guid productId, Guid? variationId, IReadOnlyCollection<OrderAdditionalSelection> additionals, DateTimeOffset instant, CancellationToken cancellationToken) { Token = cancellationToken; return Task.FromResult<OrderOfferSnapshot?>(tenantId == TenantId && establishmentId == EstablishmentId && productId == ProductId ? new(ProductId, null, "Produto", null, price ?? new Money(10), []) : null); } }
    private sealed class CustomerResolver : IOrderCustomerResolver
    { public static readonly Guid CustomerId = Guid.Parse("55555555-5555-5555-5555-555555555555"); public Guid TenantId { get; private set; } public Task<OrderCustomerSnapshot?> ResolveAsync(Guid tenantId, Guid establishmentId, Guid customerId, Guid? addressId, CancellationToken cancellationToken) { TenantId = tenantId; return Task.FromResult<OrderCustomerSnapshot?>(new(CustomerId, "Maria", "11999998888", null, null)); } }
    private sealed class TableResolver : IOrderTableResolver
    { public static readonly Guid TableId = Guid.Parse("66666666-6666-6666-6666-666666666666"); public Guid EstablishmentId { get; private set; } public Task<OrderTableSnapshot?> ResolveActiveAsync(Guid tenantId, Guid establishmentId, Guid tableId, CancellationToken cancellationToken) { EstablishmentId = establishmentId; return Task.FromResult<OrderTableSnapshot?>(new(TableId, "01")); } }
    private sealed class Sequence : IOrderNumberSequence
    { public bool Called { get; private set; } public Task<long> ReserveAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken) { Called = true; return Task.FromResult(7L); } }
    private sealed class Transaction(bool failAfterOperation = false) : IOrderConfirmationTransaction
    { public bool Executed { get; private set; } public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken) { Executed = true; await operation(cancellationToken); if (failAfterOperation) throw new InvalidOperationException("Transaction rolled back."); } }
    private sealed class Publisher : IOrderUpdatePublisher
    { public List<OrderUpdateSignal> Signals { get; } = []; public Task PublishAsync(Guid tenantId, OrderUpdateSignal signal, CancellationToken cancellationToken) { Assert.Equal(TenantId, tenantId); Signals.Add(signal); return Task.CompletedTask; } }
    private sealed class CustomerNotificationStager : ICustomerOrderNotificationStager { public Task StageAsync(Order order, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class ReadGateway : IOrderReadGateway
    { public Guid TenantId { get; private set; } public Guid EstablishmentId { get; private set; } public Task<OrderReadModel?> GetAsync(Guid tenantId, Guid establishmentId, Guid orderId, CancellationToken cancellationToken) { TenantId = tenantId; EstablishmentId = establishmentId; return Task.FromResult<OrderReadModel?>(new(orderId, null, null, OrderServiceType.Pickup, OrderStatus.Draft, null, null, null, null, 0, 0, 0, 0, null, 0, 0, false, [], [])); } public Task<OrderSearchResult> SearchAsync(Guid tenantId, Guid establishmentId, DateTimeOffset? from, DateTimeOffset? to, OrderStatus? status, long? number, OrderServiceType? serviceType, int page, int pageSize, CancellationToken cancellationToken)=>Task.FromResult(new OrderSearchResult(0,[])); }
    private sealed class CouponRepository : ICouponRepository
    { public Task<Coupon?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) => Task.FromResult<Coupon?>(null); public Task<Coupon?> FindByCodeAsync(Guid tenantId, Guid establishmentId, string normalizedCode, CancellationToken cancellationToken) => Task.FromResult<Coupon?>(null); public Task AddAsync(Coupon coupon, CancellationToken cancellationToken) => Task.CompletedTask; public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class AvailableGateway : IOrderAvailabilityGateway
    { public Task<AvailabilityDecision> EvaluateAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, DateTimeOffset instant, CancellationToken cancellationToken) => Task.FromResult(AvailabilityDecision.Available()); }
}
