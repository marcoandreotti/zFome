using ZFome.Domain.Exceptions;
using ZFome.Domain.Operations;
using ZFome.Domain.Ordering;

namespace ZFome.Domain.Tests.Operations;

public sealed class OrderSchedulingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid EstablishmentId = Guid.NewGuid();

    [Fact]
    public void Policy_uses_approved_defaults_and_rejects_invalid_scope_or_limits()
    {
        var policy = OrderSchedulingPolicy.Create(TenantId, EstablishmentId, OrderServiceType.Pickup, true);

        Assert.Equal(30, policy.SlotIntervalMinutes);
        Assert.Equal(60, policy.MinimumAdvanceMinutes);
        Assert.Equal(30, policy.HorizonDays);
        Assert.Null(policy.MaximumOrdersPerSlot);
        Assert.Throws<DomainException>(() => OrderSchedulingPolicy.Create(TenantId, EstablishmentId, OrderServiceType.Table, true));
        Assert.Throws<DomainException>(() => OrderSchedulingPolicy.Create(TenantId, EstablishmentId, OrderServiceType.Pickup, true, -1));
        Assert.Throws<DomainException>(() => OrderSchedulingPolicy.Create(TenantId, EstablishmentId, OrderServiceType.Pickup, true, 0, 0));
        Assert.Throws<DomainException>(() => OrderSchedulingPolicy.Create(TenantId, EstablishmentId, OrderServiceType.Pickup, true, maximumOrdersPerSlot: 0));
    }

    [Fact]
    public void Slots_respect_business_hours_minimum_advance_and_local_interval()
    {
        var policy = OrderSchedulingPolicy.Create(TenantId, EstablishmentId, OrderServiceType.Pickup, true, 60, 1);
        var hours = new[] { BusinessHours.Create(TenantId, EstablishmentId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)) };

        var slots = OrderSchedulingEvaluator.GetSlots(policy, true, "UTC", hours, [], [], new Dictionary<DateTimeOffset, int>(), Now);

        Assert.Equal(6, slots.Count);
        Assert.Equal(new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero), slots[0].StartsAt);
        Assert.All(slots, slot => Assert.Null(slot.RemainingCapacity));
    }

    [Fact]
    public void Slots_exclude_full_capacity_and_stop_at_configured_horizon()
    {
        var policy = OrderSchedulingPolicy.Create(TenantId, EstablishmentId, OrderServiceType.Pickup, true, 60, 1, 1);
        var hours = new[]
        {
            BusinessHours.Create(TenantId, EstablishmentId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0)),
            BusinessHours.Create(TenantId, EstablishmentId, DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(11, 0))
        };
        var fullSlot = Now.Date.AddHours(9);
        var reservations = new Dictionary<DateTimeOffset, int> { [fullSlot] = 1 };

        var slots = OrderSchedulingEvaluator.GetSlots(policy, true, "UTC", hours, [], [], reservations, Now);

        Assert.DoesNotContain(slots, x => x.StartsAt == fullSlot);
        Assert.All(slots, x => Assert.InRange(x.StartsAt, Now.AddMinutes(60), Now.AddDays(1)));
        Assert.All(slots, x => Assert.Equal(1, x.RemainingCapacity));
    }

    [Fact]
    public void Slots_apply_establishment_timezone_and_schedule_exceptions()
    {
        var localNow = new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.FromHours(-3));
        var policy = OrderSchedulingPolicy.Create(TenantId, EstablishmentId, OrderServiceType.Pickup, true, 0, 1);
        var hours = new[] { BusinessHours.Create(TenantId, EstablishmentId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(11, 0)) };
        var closed = ServiceScheduleException.CreateClosed(TenantId, EstablishmentId, new DateOnly(2026, 9, 28), OrderServiceType.Pickup, "Feriado");

        var slots = OrderSchedulingEvaluator.GetSlots(policy, true, "America/Sao_Paulo", hours, [closed], [], new Dictionary<DateTimeOffset, int>(), localNow);

        Assert.Empty(slots);
    }
}
