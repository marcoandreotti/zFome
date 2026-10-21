using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Promotions;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Ordering;
using ZFome.Application.Promotions;
using ZFome.Application.Tenancy;
using ZFome.Domain.Ordering;
using ZFome.Domain.Promotions;
using ZFome.Domain.SharedKernel;
using ZFome.Domain.Operations;

namespace ZFome.Application.Tests.Promotions;

public sealed class CouponApplicationTests
{
    private static readonly Guid TenantId = Guid.NewGuid(); private static readonly Guid UserId = Guid.NewGuid(); private static readonly Guid EstablishmentId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Upsert_normalizes_code_in_authenticated_scope()
    {
        var repository = new Coupons(); var handler = new UpsertCouponCommandHandler(Resolver(), repository, new Clock());
        var id = await handler.HandleAsync(new(EstablishmentId, null, " promo-10 ", null, CouponDiscountType.Percentage, 10, 20, Now.AddDays(-1), Now.AddDays(1), 5), CancellationToken.None);
        Assert.Equal(id, repository.Value!.Id); Assert.Equal("PROMO10", repository.Value.Code); Assert.Equal(TenantId, repository.Value.TenantId);
    }

    [Fact]
    public async Task Apply_and_remove_coupon_recalculate_draft_without_consuming()
    {
        var order = Draft(); var coupon = Coupon.Create(TenantId, EstablishmentId, "SAVE", null, CouponDiscountType.FixedAmount, 5, Money.Zero, Now.AddDays(-1), Now.AddDays(1), 1, Now);
        var orders = new Orders(order); var coupons = new Coupons(coupon);
        await new ApplyCouponCommandHandler(Resolver(), orders, coupons, new Clock()).HandleAsync(new(EstablishmentId, order.Id, "save"), CancellationToken.None);
        Assert.Equal(15m, order.Total.Amount); Assert.Equal(0, coupon.UsedCount);
        await new RemoveCouponCommandHandler(Resolver(), orders, new Clock()).HandleAsync(new(EstablishmentId, order.Id), CancellationToken.None);
        Assert.Equal(20m, order.Total.Amount); Assert.Null(order.CouponId);
    }

    [Fact]
    public async Task Confirmation_revalidates_and_consumes_coupon_in_transaction()
    {
        var order = Draft(); var coupon = Coupon.Create(TenantId, EstablishmentId, "SAVE", null, CouponDiscountType.FixedAmount, 5, Money.Zero, Now.AddDays(-1), Now.AddDays(1), 1, Now);
        order.ApplyCoupon(coupon.Id, coupon.Code, coupon.Evaluate(order.Subtotal, Now).Discount, Now); var transaction = new Transaction();
        var handler = new ConfirmOrderCommandHandler(Resolver(), new Orders(order), new Offers(), new Sequence(), transaction, new Coupons(coupon), new AvailableGateway(), new Publisher(), new Clock());
        await handler.HandleAsync(new(EstablishmentId, order.Id), CancellationToken.None);
        Assert.True(transaction.Executed); Assert.Equal(1, coupon.UsedCount); Assert.Equal(order.Id, Assert.Single(coupon.Uses).OrderId); Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public async Task Validators_reject_invalid_coupon_inputs()
    {
        Assert.False((await new UpsertCouponCommandValidator().ValidateAsync(new UpsertCouponCommand(Guid.Empty, null, "", null, CouponDiscountType.Percentage, 0, -1, Now, Now, 0))).IsValid);
        Assert.False((await new ApplyCouponCommandValidator().ValidateAsync(new ApplyCouponCommand(Guid.Empty, Guid.Empty, ""))).IsValid);
    }

    private static Order Draft() { var order = Order.Create(TenantId, EstablishmentId, OrderServiceType.Pickup, null, null, null, null, null, Now); order.AddItem(Offers.ProductId, null, "Produto", null, new Money(20), new Quantity(1), [], null, Now); return order; }
    private static EstablishmentScopeResolver Resolver() => new(new Context(), new Access());
    private sealed class Context : ITenantContext { public bool HasTenant => true; public Guid TenantId => CouponApplicationTests.TenantId; public bool HasUser => true; public Guid UserId => CouponApplicationTests.UserId; public Guid GetRequiredTenantId() => TenantId; public Guid GetRequiredUserId() => UserId; }
    private sealed class Access : IEstablishmentAccessGateway { public Task<bool> HasActiveAccessAsync(Guid tenantId, Guid userId, Guid establishmentId, CancellationToken cancellationToken) => Task.FromResult(tenantId == TenantId && establishmentId == EstablishmentId); }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Coupons(Coupon? value = null) : ICouponRepository { public Coupon? Value { get; private set; } = value; public Task<Coupon?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) => Task.FromResult(Value is { } coupon && coupon.Id == id && coupon.TenantId == tenantId && coupon.EstablishmentId == establishmentId ? coupon : null); public Task<Coupon?> FindByCodeAsync(Guid tenantId, Guid establishmentId, string normalizedCode, CancellationToken cancellationToken) => Task.FromResult(Value is { } coupon && coupon.Code == normalizedCode && coupon.TenantId == tenantId && coupon.EstablishmentId == establishmentId ? coupon : null); public Task AddAsync(Coupon coupon, CancellationToken cancellationToken) { Value = coupon; return Task.CompletedTask; } public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class Orders(Order value) : IOrderRepository { public Task<Order?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) => Task.FromResult<Order?>(value.TenantId == tenantId && value.EstablishmentId == establishmentId && value.Id == id ? value : null); public Task AddAsync(Order order, CancellationToken cancellationToken) => Task.CompletedTask; public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class Offers : IOrderOfferResolver { public static readonly Guid ProductId = Guid.NewGuid(); public Task<OrderOfferSnapshot?> ResolveAsync(Guid tenantId, Guid establishmentId, Guid productId, Guid? variationId, IReadOnlyCollection<OrderAdditionalSelection> additionals, DateTimeOffset instant, CancellationToken cancellationToken) => Task.FromResult<OrderOfferSnapshot?>(new(ProductId, null, "Produto", null, new Money(20), [])); }
    private sealed class Sequence : IOrderNumberSequence { public Task<long> ReserveAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken) => Task.FromResult(1L); }
    private sealed class Transaction : IOrderConfirmationTransaction { public bool Executed { get; private set; } public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken) { Executed = true; await operation(cancellationToken); } }
    private sealed class AvailableGateway : IOrderAvailabilityGateway { public Task<AvailabilityDecision> EvaluateAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, DateTimeOffset instant, CancellationToken cancellationToken) => Task.FromResult(AvailabilityDecision.Available()); }
    private sealed class Publisher : IOrderUpdatePublisher { public Task PublishAsync(Guid tenantId, OrderUpdateSignal signal, CancellationToken cancellationToken) => Task.CompletedTask; }
}
