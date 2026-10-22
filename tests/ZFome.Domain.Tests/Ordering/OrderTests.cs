using ZFome.Domain.Exceptions;
using ZFome.Domain.Ordering;
using ZFome.Domain.SharedKernel;
using ZFome.Domain.Delivery;

namespace ZFome.Domain.Tests.Ordering;

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Draft_preserves_commercial_snapshots_and_calculates_authoritative_total()
    {
        var order = Create(OrderServiceType.Pickup);
        var item = order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Pizza", "Grande", new Money(20.005m), new Quantity(2),
            [new(Guid.NewGuid(), "Bacon", new Money(2.335m), new Quantity(2))], null, Now);
        order.SetAdjustments(new Money(5), new Money(3), Now);

        Assert.Equal("Pizza", item.ProductName);
        Assert.Equal(49.38m, order.Subtotal.Amount);
        Assert.Equal(47.38m, order.Total.Amount);
    }

    [Fact]
    public void Draft_rejects_negative_total_and_incompatible_service_data()
    {
        var order = Create(OrderServiceType.Pickup);
        order.AddItem(Guid.NewGuid(), null, "Item", null, new Money(10), new Quantity(1), [], null, Now);

        Assert.Throws<DomainException>(() => order.SetAdjustments(new Money(11), Money.Zero, Now));
        Assert.Throws<DomainException>(() => Order.Create(Guid.NewGuid(), Guid.NewGuid(), OrderServiceType.Table, null, null, null, null, null, Now));
    }

    [Fact]
    public void Confirmation_assigns_number_public_reference_and_immutable_history()
    {
        var order = Create(OrderServiceType.Pickup);
        order.AddItem(Guid.NewGuid(), null, "Item", null, new Money(10), new Quantity(1), [], null, Now);
        order.Confirm(42, Now, null);

        Assert.Equal(42, order.Number);
        Assert.Equal(48, order.PublicReference!.Length);
        var entry = Assert.Single(order.History);
        Assert.Equal(OrderStatus.Draft, entry.PreviousStatus);
        Assert.Equal(OrderStatus.Confirmed, entry.NewStatus);
        Assert.Null(entry.ActorId);
        Assert.Throws<DomainException>(() => order.AddItem(Guid.NewGuid(), null, "Other", null, new Money(1), new Quantity(1), [], null, Now));
    }

    [Fact]
    public void Scheduled_order_preserves_utc_instant_and_establishment_time_zone()
    {
        var order = Create(OrderServiceType.Pickup);
        var promisedAt = new DateTimeOffset(2026, 9, 28, 18, 30, 0, TimeSpan.FromHours(-3));

        order.Schedule(promisedAt, "America/Sao_Paulo", Now);

        Assert.True(order.IsScheduled);
        Assert.Equal(promisedAt.ToUniversalTime(), order.ScheduledAtUtc);
        Assert.Equal("America/Sao_Paulo", order.ScheduledTimeZoneId);
    }

    [Fact]
    public void Schedule_rejects_table_service_non_future_time_and_invalid_time_zone()
    {
        Assert.Throws<DomainException>(() => Create(OrderServiceType.Table).Schedule(Now.AddHours(1), "UTC", Now));
        Assert.Throws<DomainException>(() => Create(OrderServiceType.Pickup).Schedule(Now, "UTC", Now));
        Assert.Throws<DomainException>(() => Create(OrderServiceType.Delivery).Schedule(Now.AddHours(1), "invalid-zone", Now));
    }

    [Theory]
    [InlineData(OrderServiceType.Table)]
    [InlineData(OrderServiceType.Pickup)]
    public void Non_delivery_follows_preparation_ready_completion(OrderServiceType serviceType)
    {
        var order = Confirmed(serviceType);
        var actor = Guid.NewGuid();
        order.StartPreparation(Now.AddMinutes(1), actor);
        order.MarkReady(Now.AddMinutes(2), actor);
        order.Complete(Now.AddMinutes(3), actor);

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(4, order.History.Count);
    }

    [Fact]
    public void Delivery_requires_dispatch_and_rejects_invalid_or_terminal_transitions()
    {
        var order = Confirmed(OrderServiceType.Delivery);
        var actor = Guid.NewGuid();
        Assert.Throws<DomainException>(() => order.Complete(Now, actor));
        order.StartPreparation(Now, actor);
        order.MarkReady(Now, actor);
        order.Dispatch(Now, actor);
        order.Complete(Now, actor);
        Assert.Throws<DomainException>(() => order.Cancel(Now, actor));
    }

    [Fact]
    public void Delivery_policy_is_snapshotted_and_does_not_change_with_later_policy_edits()
    {
        var order = Create(OrderServiceType.Delivery);
        order.AddItem(Guid.NewGuid(), null, "Item", null, new Money(20), new Quantity(1), [], null, Now);
        var regionId = Guid.NewGuid();
        order.ApplyDeliveryQuote(new DeliveryQuote(regionId, "Centro", new Money(6.50m), 35), Now);

        Assert.Equal(regionId, order.DeliveryRegionId);
        Assert.Equal("Centro", order.DeliveryRegionName);
        Assert.Equal(6.50m, order.DeliveryFee.Amount);
        Assert.Equal(35, order.DeliveryEstimatedMinutes);
        Assert.Equal(26.50m, order.Total.Amount);
    }

    [Fact]
    public void Cancellation_and_rejection_capture_optional_actor_and_note()
    {
        var cancelled = Confirmed(OrderServiceType.Pickup);
        cancelled.Cancel(Now, null, "cliente desistiu");
        Assert.Equal("cliente desistiu", cancelled.History.Last().Note);

        var rejected = Confirmed(OrderServiceType.Pickup);
        var actor = Guid.NewGuid();
        rejected.Reject(Now, actor, "indisponível");
        Assert.Equal(actor, rejected.History.Last().ActorId);
    }

    [Fact]
    public void Coupon_snapshot_caps_discount_and_removal_restores_draft_total()
    {
        var order = Create(OrderServiceType.Pickup); order.AddItem(Guid.NewGuid(), null, "Item", null, new Money(30), new Quantity(1), [], null, Now);
        var couponId = Guid.NewGuid(); order.ApplyCoupon(couponId, "PROMO", new Money(100), Now);
        Assert.Equal(0m, order.Total.Amount); Assert.Equal(couponId, order.CouponId); Assert.Equal("PROMO", order.CouponCode);
        order.RemoveCoupon(Now); Assert.Equal(30m, order.Total.Amount); Assert.Null(order.CouponId);
    }

    private static Order Confirmed(OrderServiceType serviceType)
    {
        var order = Create(serviceType);
        order.AddItem(Guid.NewGuid(), null, "Item", null, new Money(10), new Quantity(1), [], null, Now);
        order.Confirm(1, Now);
        return order;
    }

    private static Order Create(OrderServiceType serviceType) => Order.Create(
        Guid.NewGuid(), Guid.NewGuid(), serviceType, null, null, null,
        serviceType == OrderServiceType.Table ? Guid.NewGuid() : null,
        serviceType == OrderServiceType.Delivery ? new("Rua A", "1", null, "Centro", "São Paulo", "SP", "01001000") : null,
        Now);
}
