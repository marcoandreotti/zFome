using ZFome.Domain.Exceptions;
using ZFome.Domain.Ordering;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Operations;

public enum AvailabilityReason
{
    Available,
    EstablishmentInactive,
    CalendarException,
    ServicePaused,
    OutsideBusinessHours,
    OfferUnavailable
}

public sealed record AvailabilityDecision(bool IsAvailable, AvailabilityReason Reason, string? Message, DateTimeOffset? NextOpening)
{
    public static AvailabilityDecision Available() => new(true, AvailabilityReason.Available, null, null);
}

public sealed class ServiceScheduleException : IEstablishmentScopedEntity
{
    private ServiceScheduleException() { }

    private ServiceScheduleException(Guid tenantId, Guid establishmentId, DateOnly date, OrderServiceType? serviceType, bool isOpen, TimeOnly? opensAt, TimeOnly? closesAt, string? reason)
    {
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty || serviceType is { } mode && !Enum.IsDefined(mode))
            throw new DomainException("Schedule-exception scope is invalid.");
        if (isOpen && (opensAt is null || closesAt is null || opensAt == closesAt) || !isOpen && (opensAt is not null || closesAt is not null))
            throw new DomainException("Schedule-exception interval is invalid.");
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EstablishmentId = establishmentId;
        Date = date;
        ServiceType = serviceType;
        IsOpen = isOpen;
        OpensAt = opensAt;
        ClosesAt = closesAt;
        Reason = Normalize(reason);
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public DateOnly Date { get; private set; }
    public OrderServiceType? ServiceType { get; private set; }
    public bool IsOpen { get; private set; }
    public TimeOnly? OpensAt { get; private set; }
    public TimeOnly? ClosesAt { get; private set; }
    public string? Reason { get; private set; }
    public bool CrossesMidnight => IsOpen && ClosesAt <= OpensAt;

    public static ServiceScheduleException CreateClosed(Guid tenantId, Guid establishmentId, DateOnly date, OrderServiceType? serviceType, string? reason) =>
        new(tenantId, establishmentId, date, serviceType, false, null, null, reason);

    public static ServiceScheduleException CreateOpen(Guid tenantId, Guid establishmentId, DateOnly date, OrderServiceType? serviceType, TimeOnly opensAt, TimeOnly closesAt, string? reason) =>
        new(tenantId, establishmentId, date, serviceType, true, opensAt, closesAt, reason);

    public bool AppliesTo(OrderServiceType serviceType) => ServiceType is null || ServiceType == serviceType;

    public bool Contains(DateTime localDateTime)
    {
        if (!IsOpen) return false;
        var date = DateOnly.FromDateTime(localDateTime);
        var time = TimeOnly.FromDateTime(localDateTime);
        if (!CrossesMidnight) return date == Date && time >= OpensAt && time < ClosesAt;
        return date == Date && time >= OpensAt || date == Date.AddDays(1) && time < ClosesAt;
    }

    private static string? Normalize(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        var value = reason.Trim();
        if (value.Length > 250) throw new DomainException("Availability reason is too long.");
        return value;
    }
}

public sealed class ServicePause : IEstablishmentScopedEntity
{
    private ServicePause() { }

    private ServicePause(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, DateTimeOffset startsAt, DateTimeOffset? endsAt, string? reason)
    {
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty || !Enum.IsDefined(serviceType) || endsAt <= startsAt)
            throw new DomainException("Service pause is invalid.");
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EstablishmentId = establishmentId;
        ServiceType = serviceType;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (Reason?.Length > 250) throw new DomainException("Availability reason is too long.");
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public OrderServiceType ServiceType { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset? EndsAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? Reason { get; private set; }

    public static ServicePause Create(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, DateTimeOffset startsAt, DateTimeOffset? endsAt, string? reason) =>
        new(tenantId, establishmentId, serviceType, startsAt, endsAt, reason);

    public bool IsActiveAt(DateTimeOffset instant) => CancelledAt is null && instant >= StartsAt && (EndsAt is null || instant < EndsAt);

    public void Cancel(DateTimeOffset now)
    {
        if (now < StartsAt) throw new DomainException("A pause cannot be cancelled before it starts.");
        CancelledAt ??= now;
    }
}

public static class AvailabilityEvaluator
{
    public static AvailabilityDecision Evaluate(
        bool establishmentActive,
        string timeZoneId,
        IReadOnlyCollection<BusinessHours> hours,
        IReadOnlyCollection<ServiceScheduleException> exceptions,
        IReadOnlyCollection<ServicePause> pauses,
        OrderServiceType serviceType,
        DateTimeOffset instant)
    {
        var zone = ResolveTimeZone(timeZoneId);
        var current = EvaluateCore(establishmentActive, zone, hours, exceptions, pauses, serviceType, instant);
        if (current.IsAvailable) return current;
        var next = current.Reason is AvailabilityReason.EstablishmentInactive or AvailabilityReason.ServicePaused
            && pauses.Any(x => x.ServiceType == serviceType && x.IsActiveAt(instant) && x.EndsAt is null)
            ? null
            : FindNextOpening(establishmentActive, zone, hours, exceptions, pauses, serviceType, instant);
        return current with { NextOpening = next };
    }

    private static AvailabilityDecision EvaluateCore(
        bool establishmentActive,
        TimeZoneInfo zone,
        IReadOnlyCollection<BusinessHours> hours,
        IReadOnlyCollection<ServiceScheduleException> exceptions,
        IReadOnlyCollection<ServicePause> pauses,
        OrderServiceType serviceType,
        DateTimeOffset instant)
    {
        if (!establishmentActive)
            return new(false, AvailabilityReason.EstablishmentInactive, "A unidade não está recebendo pedidos.", null);

        var local = TimeZoneInfo.ConvertTime(instant, zone).DateTime;
        var today = SelectException(exceptions, DateOnly.FromDateTime(local), serviceType);
        if (today is { IsOpen: false })
            return new(false, AvailabilityReason.CalendarException, today.Reason ?? "A unidade está fechada excepcionalmente.", null);

        var activePause = pauses
            .Where(x => x.ServiceType == serviceType && x.IsActiveAt(instant))
            .OrderByDescending(x => x.StartsAt)
            .FirstOrDefault();
        if (activePause is not null)
            return new(false, AvailabilityReason.ServicePaused, activePause.Reason ?? "Esta modalidade está temporariamente pausada.", activePause.EndsAt);

        var previous = SelectException(exceptions, DateOnly.FromDateTime(local).AddDays(-1), serviceType);
        var openException = today is { IsOpen: true } ? today : previous is { IsOpen: true } && previous.Contains(local) ? previous : null;
        if (openException is not null)
            return openException.Contains(local)
                ? AvailabilityDecision.Available()
                : new(false, AvailabilityReason.CalendarException, openException.Reason ?? "Esta modalidade está fora do horário excepcional de atendimento.", null);

        return hours.Any(x => x.Contains(local))
            ? AvailabilityDecision.Available()
            : new(false, AvailabilityReason.OutsideBusinessHours, "Estamos fora do horário de atendimento.", null);
    }

    private static DateTimeOffset? FindNextOpening(
        bool establishmentActive,
        TimeZoneInfo zone,
        IReadOnlyCollection<BusinessHours> hours,
        IReadOnlyCollection<ServiceScheduleException> exceptions,
        IReadOnlyCollection<ServicePause> pauses,
        OrderServiceType serviceType,
        DateTimeOffset instant)
    {
        var localNow = TimeZoneInfo.ConvertTime(instant, zone).DateTime;
        var candidates = new List<DateTimeOffset>();
        candidates.AddRange(pauses.Where(x => x.ServiceType == serviceType && x.IsActiveAt(instant) && x.EndsAt > instant).Select(x => x.EndsAt!.Value));
        for (var offset = 0; offset <= 14; offset++)
        {
            var date = DateOnly.FromDateTime(localNow).AddDays(offset);
            var exception = SelectException(exceptions, date, serviceType);
            if (exception is { IsOpen: true })
                AddCandidate(candidates, date, exception.OpensAt!.Value, zone, instant);
            else if (exception is null)
                foreach (var item in hours.Where(x => x.IsActive && x.DayOfWeek == date.DayOfWeek))
                    AddCandidate(candidates, date, item.OpensAt, zone, instant);
        }

        return candidates.Distinct().Order().FirstOrDefault(candidate =>
            EvaluateCore(establishmentActive, zone, hours, exceptions, pauses, serviceType, candidate).IsAvailable) is { } found && found != default
            ? found
            : null;
    }

    private static ServiceScheduleException? SelectException(IEnumerable<ServiceScheduleException> exceptions, DateOnly date, OrderServiceType serviceType) =>
        exceptions.Where(x => x.Date == date && x.AppliesTo(serviceType))
            .OrderByDescending(x => x.ServiceType == serviceType)
            .ThenBy(x => x.Id)
            .FirstOrDefault();

    private static void AddCandidate(ICollection<DateTimeOffset> candidates, DateOnly date, TimeOnly time, TimeZoneInfo zone, DateTimeOffset instant)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) return;
        var candidate = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone));
        if (candidate > instant) candidates.Add(candidate);
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { throw new DomainException("Time zone is invalid."); }
        catch (InvalidTimeZoneException) { throw new DomainException("Time zone is invalid."); }
    }
}
