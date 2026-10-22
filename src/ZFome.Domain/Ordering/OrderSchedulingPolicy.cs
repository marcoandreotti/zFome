using ZFome.Domain.Exceptions;
using ZFome.Domain.Operations;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Ordering;

public sealed class OrderSchedulingPolicy : IEstablishmentScopedEntity
{
    public const int DefaultSlotIntervalMinutes = 30;
    public const int DefaultMinimumAdvanceMinutes = 60;
    public const int DefaultHorizonDays = 30;
    public const int MaximumHorizonDays = 90;

    private OrderSchedulingPolicy() { }

    private OrderSchedulingPolicy(
        Guid tenantId,
        Guid establishmentId,
        OrderServiceType serviceType,
        bool isEnabled,
        int minimumAdvanceMinutes,
        int horizonDays,
        int? maximumOrdersPerSlot)
    {
        Validate(tenantId, establishmentId, serviceType, minimumAdvanceMinutes, horizonDays, maximumOrdersPerSlot);
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EstablishmentId = establishmentId;
        ServiceType = serviceType;
        IsEnabled = isEnabled;
        MinimumAdvanceMinutes = minimumAdvanceMinutes;
        HorizonDays = horizonDays;
        MaximumOrdersPerSlot = maximumOrdersPerSlot;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public OrderServiceType ServiceType { get; private set; }
    public bool IsEnabled { get; private set; }
    public int SlotIntervalMinutes => DefaultSlotIntervalMinutes;
    public int MinimumAdvanceMinutes { get; private set; }
    public int HorizonDays { get; private set; }
    public int? MaximumOrdersPerSlot { get; private set; }

    public static OrderSchedulingPolicy Create(
        Guid tenantId,
        Guid establishmentId,
        OrderServiceType serviceType,
        bool isEnabled,
        int minimumAdvanceMinutes = DefaultMinimumAdvanceMinutes,
        int horizonDays = DefaultHorizonDays,
        int? maximumOrdersPerSlot = null) =>
        new(tenantId, establishmentId, serviceType, isEnabled, minimumAdvanceMinutes, horizonDays, maximumOrdersPerSlot);

    public void Configure(bool isEnabled, int minimumAdvanceMinutes, int horizonDays, int? maximumOrdersPerSlot)
    {
        Validate(TenantId, EstablishmentId, ServiceType, minimumAdvanceMinutes, horizonDays, maximumOrdersPerSlot);
        IsEnabled = isEnabled;
        MinimumAdvanceMinutes = minimumAdvanceMinutes;
        HorizonDays = horizonDays;
        MaximumOrdersPerSlot = maximumOrdersPerSlot;
    }

    private static void Validate(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, int minimumAdvanceMinutes, int horizonDays, int? maximumOrdersPerSlot)
    {
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty || serviceType is not (OrderServiceType.Pickup or OrderServiceType.Delivery))
            throw new DomainException("Order scheduling policy scope is invalid.");
        if (minimumAdvanceMinutes < 0 || minimumAdvanceMinutes > MaximumHorizonDays * 24 * 60)
            throw new DomainException("Order scheduling minimum advance is invalid.");
        if (horizonDays is < 1 or > MaximumHorizonDays || minimumAdvanceMinutes > horizonDays * 24 * 60)
            throw new DomainException("Order scheduling horizon is invalid.");
        if (maximumOrdersPerSlot is <= 0)
            throw new DomainException("Order scheduling capacity must be positive when configured.");
    }
}

public sealed record OrderSchedulingSlot(DateTimeOffset StartsAt, int? RemainingCapacity);

public static class OrderSchedulingEvaluator
{
    public static void EnsureSlotIsValid(
        OrderSchedulingPolicy policy,
        string timeZoneId,
        DateTimeOffset selectedAt,
        DateTimeOffset now,
        int reservedOrders,
        bool availableForFullInterval)
    {
        if (!policy.IsEnabled) throw new DomainException("Scheduling is not enabled for this service.");
        if (selectedAt < now.AddMinutes(policy.MinimumAdvanceMinutes) || selectedAt > now.AddDays(policy.HorizonDays))
            throw new DomainException("Selected order slot is outside the configured scheduling window.");
        if (!availableForFullInterval) throw new DomainException("Selected order slot is outside available service hours.");
        if (policy.MaximumOrdersPerSlot is { } maximum && reservedOrders >= maximum)
            throw new DomainException("Selected order slot has reached its capacity.");

        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (TimeZoneNotFoundException) { throw new DomainException("Time zone is invalid."); }
        catch (InvalidTimeZoneException) { throw new DomainException("Time zone is invalid."); }

        var local = TimeZoneInfo.ConvertTime(selectedAt, zone).DateTime;
        if (local.TimeOfDay.Ticks % TimeSpan.FromMinutes(policy.SlotIntervalMinutes).Ticks != 0)
            throw new DomainException("Selected order time is not aligned to a configured slot.");
    }

    public static IReadOnlyList<OrderSchedulingSlot> GetSlots(
        OrderSchedulingPolicy policy,
        bool establishmentActive,
        string timeZoneId,
        IReadOnlyCollection<BusinessHours> hours,
        IReadOnlyCollection<ServiceScheduleException> exceptions,
        IReadOnlyCollection<ServicePause> pauses,
        IReadOnlyDictionary<DateTimeOffset, int> reservedOrders,
        DateTimeOffset now)
    {
        if (!policy.IsEnabled || !establishmentActive) return [];

        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (TimeZoneNotFoundException) { throw new DomainException("Time zone is invalid."); }
        catch (InvalidTimeZoneException) { throw new DomainException("Time zone is invalid."); }

        var earliest = now.AddMinutes(policy.MinimumAdvanceMinutes);
        var latest = now.AddDays(policy.HorizonDays);
        var localFirst = TimeZoneInfo.ConvertTime(earliest, zone).Date;
        var localLast = TimeZoneInfo.ConvertTime(latest, zone).Date;
        var slots = new List<OrderSchedulingSlot>();

        for (var date = localFirst; date <= localLast; date = date.AddDays(1))
        {
            for (var minutes = 0; minutes < 24 * 60; minutes += policy.SlotIntervalMinutes)
            {
                var local = DateTime.SpecifyKind(date.AddMinutes(minutes), DateTimeKind.Unspecified);
                if (zone.IsInvalidTime(local)) continue;

                var start = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
                if (start < earliest || start > latest) continue;

                var end = start.AddMinutes(policy.SlotIntervalMinutes).AddTicks(-1);
                var opensAtStart = AvailabilityEvaluator.Evaluate(establishmentActive, timeZoneId, hours, exceptions, pauses, policy.ServiceType, start).IsAvailable;
                var opensAtEnd = AvailabilityEvaluator.Evaluate(establishmentActive, timeZoneId, hours, exceptions, pauses, policy.ServiceType, end).IsAvailable;
                if (!opensAtStart || !opensAtEnd) continue;

                var reserved = reservedOrders.TryGetValue(start, out var count) ? count : 0;
                if (policy.MaximumOrdersPerSlot is { } maximum && reserved >= maximum) continue;
                slots.Add(new(start, policy.MaximumOrdersPerSlot is { } limit ? limit - reserved : null));
            }
        }

        return slots;
    }
}
