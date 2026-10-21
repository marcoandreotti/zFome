using ZFome.Domain.Exceptions;
using ZFome.Domain.Operations;
using ZFome.Domain.Ordering;

namespace ZFome.Domain.Tests.Operations;

public sealed class OperationsTests
{
    [Fact]
    public void Table_token_is_opaque_unique_and_revocable()
    {
        var table = ServiceTable.Create(Guid.NewGuid(), Guid.NewGuid(), " mesa-1 ");
        var token = table.QrCodeToken;
        Assert.Equal("MESA-1", table.Code); Assert.Equal(64, token.Length);
        table.RevokeToken(); Assert.NotEqual(token, table.QrCodeToken);
    }

    [Fact]
    public void Hours_support_midnight_and_reject_overlaps()
    {
        var tenant = Guid.NewGuid(); var establishment = Guid.NewGuid();
        var overnight = BusinessHours.Create(tenant, establishment, DayOfWeek.Monday, new TimeOnly(22, 0), new TimeOnly(2, 0));
        var overlapping = BusinessHours.Create(tenant, establishment, DayOfWeek.Tuesday, new TimeOnly(1, 0), new TimeOnly(3, 0));
        Assert.True(overnight.Contains(new DateTime(2026, 9, 21, 23, 0, 0)));
        Assert.True(overnight.Contains(new DateTime(2026, 9, 22, 1, 0, 0)));
        Assert.Throws<DomainException>(() => BusinessHours.EnsureNoOverlaps([overnight, overlapping]));
        Assert.Throws<DomainException>(() => BusinessHours.Create(tenant, establishment, DayOfWeek.Monday, new TimeOnly(2, 0), new TimeOnly(2, 0)));
    }

    [Fact]
    public void Availability_has_deterministic_precedence_and_next_opening()
    {
        var tenant = Guid.NewGuid(); var establishment = Guid.NewGuid();
        var hours = new[] { BusinessHours.Create(tenant, establishment, DayOfWeek.Monday, new(11, 0), new(14, 0)) };
        var monday = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
        var closed = ServiceScheduleException.CreateClosed(tenant, establishment, new(2026, 9, 21), OrderServiceType.Pickup, "Holiday");
        var pause = ServicePause.Create(tenant, establishment, OrderServiceType.Pickup, monday.AddHours(-1), null, "Kitchen pause");

        var exceptionDecision = AvailabilityEvaluator.Evaluate(true, "UTC", hours, [closed], [pause], OrderServiceType.Pickup, monday);
        Assert.Equal(AvailabilityReason.CalendarException, exceptionDecision.Reason);

        var pauseDecision = AvailabilityEvaluator.Evaluate(true, "UTC", hours, [], [pause], OrderServiceType.Pickup, monday);
        Assert.Equal(AvailabilityReason.ServicePaused, pauseDecision.Reason);

        var beforeOpening = AvailabilityEvaluator.Evaluate(true, "UTC", hours, [], [], OrderServiceType.Pickup, monday.AddHours(-2));
        Assert.Equal(new DateTimeOffset(2026, 9, 21, 11, 0, 0, TimeSpan.Zero), beforeOpening.NextOpening);

        var outsideHours = AvailabilityEvaluator.Evaluate(true, "UTC", [], [], [], OrderServiceType.Pickup, monday);
        Assert.Equal("Estamos fora do horário de atendimento.", outsideHours.Message);
    }

    [Fact]
    public void Open_exception_handles_midnight_but_pause_still_blocks()
    {
        var tenant = Guid.NewGuid(); var establishment = Guid.NewGuid();
        var exception = ServiceScheduleException.CreateOpen(tenant, establishment, new(2026, 9, 21), OrderServiceType.Delivery, new(22, 0), new(2, 0), null);
        var instant = new DateTimeOffset(2026, 9, 22, 1, 0, 0, TimeSpan.Zero);
        Assert.True(AvailabilityEvaluator.Evaluate(true, "UTC", [], [exception], [], OrderServiceType.Delivery, instant).IsAvailable);
        var pause = ServicePause.Create(tenant, establishment, OrderServiceType.Delivery, instant.AddMinutes(-10), instant.AddMinutes(10), null);
        Assert.Equal(AvailabilityReason.ServicePaused, AvailabilityEvaluator.Evaluate(true, "UTC", [], [exception], [pause], OrderServiceType.Delivery, instant).Reason);
    }

    [Fact]
    public void Availability_converts_utc_to_establishment_time_zone()
    {
        var tenant = Guid.NewGuid(); var establishment = Guid.NewGuid();
        var hours = new[] { BusinessHours.Create(tenant, establishment, DayOfWeek.Monday, new(11, 0), new(12, 0)) };
        var instant = new DateTimeOffset(2026, 9, 21, 14, 30, 0, TimeSpan.Zero);
        Assert.True(AvailabilityEvaluator.Evaluate(true, "America/Sao_Paulo", hours, [], [], OrderServiceType.Table, instant).IsAvailable);
    }
}
