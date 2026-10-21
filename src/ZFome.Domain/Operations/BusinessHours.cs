using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Operations;

public sealed class BusinessHours : IEstablishmentScopedEntity
{
    private const int MinutesPerDay = 24 * 60;
    private const int MinutesPerWeek = 7 * MinutesPerDay;

    private BusinessHours() { }

    private BusinessHours(Guid tenantId, Guid establishmentId, DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt)
    {
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty || !Enum.IsDefined(dayOfWeek) || closesAt == opensAt)
            throw new DomainException("Business-hours interval is invalid.");
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EstablishmentId = establishmentId;
        DayOfWeek = dayOfWeek;
        OpensAt = opensAt;
        ClosesAt = closesAt;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly OpensAt { get; private set; }
    public TimeOnly ClosesAt { get; private set; }
    public bool IsActive { get; private set; }
    public bool CrossesMidnight => ClosesAt <= OpensAt;

    public static BusinessHours Create(Guid tenantId, Guid establishmentId, DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt) =>
        new(tenantId, establishmentId, dayOfWeek, opensAt, closesAt);

    public bool Contains(DateTime localDateTime)
    {
        if (!IsActive) return false;
        var time = TimeOnly.FromDateTime(localDateTime);
        if (!CrossesMidnight) return localDateTime.DayOfWeek == DayOfWeek && time >= OpensAt && time < ClosesAt;
        return localDateTime.DayOfWeek == DayOfWeek && time >= OpensAt
            || localDateTime.DayOfWeek == Next(DayOfWeek) && time < ClosesAt;
    }

    public bool Contains(DayOfWeek day, TimeOnly time) =>
        Contains(new DateTime(2026, 9, 20).AddDays(((int)day + 7) % 7).Add(time.ToTimeSpan()));

    public void Deactivate() => IsActive = false;

    public static void EnsureNoOverlaps(IReadOnlyCollection<BusinessHours> values)
    {
        var active = values.Where(x => x.IsActive).ToArray();
        for (var i = 0; i < active.Length; i++)
        for (var j = i + 1; j < active.Length; j++)
        {
            var first = active[i].ToWeeklyInterval();
            var second = active[j].ToWeeklyInterval();
            if (new[] { -MinutesPerWeek, 0, MinutesPerWeek }.Any(shift =>
                    first.Start < second.End + shift && second.Start + shift < first.End))
                throw new DomainException("Business-hours intervals cannot overlap.");
        }
    }

    private (int Start, int End) ToWeeklyInterval()
    {
        var start = (int)DayOfWeek * MinutesPerDay + OpensAt.Hour * 60 + OpensAt.Minute;
        var end = (int)DayOfWeek * MinutesPerDay + ClosesAt.Hour * 60 + ClosesAt.Minute;
        if (CrossesMidnight) end += MinutesPerDay;
        return (start, end);
    }

    private static DayOfWeek Next(DayOfWeek value) => (DayOfWeek)(((int)value + 1) % 7);
}
