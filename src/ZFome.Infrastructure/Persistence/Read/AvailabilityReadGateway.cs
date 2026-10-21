using Dapper;
using ZFome.Application.Abstractions.Operations;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Domain.Operations;
using ZFome.Domain.Ordering;

namespace ZFome.Infrastructure.Persistence.Read;

public sealed class AvailabilityReadGateway(IReadConnectionFactory connectionFactory) : IAvailabilityReadGateway
{
    public async Task<AvailabilityDecision> EvaluateAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, DateTimeOffset instant, CancellationToken cancellationToken)
    {
        const string sql = """
            select e.is_active and t.is_active as IsActive, e.time_zone_id as TimeZoneId
            from tenancy.establishment e join tenancy.tenant t on t.id=e.tenant_id
            where e.tenant_id=@TenantId and e.id=@EstablishmentId;
            select id,tenant_id as TenantId,establishment_id as EstablishmentId,day_of_week as DayOfWeek,
                   opens_at as OpensAt,closes_at as ClosesAt,is_active as IsActive
            from operations.business_hours where tenant_id=@TenantId and establishment_id=@EstablishmentId and is_active;
            select id,tenant_id as TenantId,establishment_id as EstablishmentId,date,service_type as ServiceType,
                   is_open as IsOpen,opens_at as OpensAt,closes_at as ClosesAt,reason
            from operations.service_schedule_exception
            where tenant_id=@TenantId and establishment_id=@EstablishmentId and date between @FromDate and @ToDate;
            select id,tenant_id as TenantId,establishment_id as EstablishmentId,service_type as ServiceType,
                   starts_at as StartsAt,ends_at as EndsAt,cancelled_at as CancelledAt,reason
            from operations.service_pause
            where tenant_id=@TenantId and establishment_id=@EstablishmentId and cancelled_at is null
              and starts_at<=@Horizon and (ends_at is null or ends_at>@Instant);
            """;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var parameters = new { TenantId = tenantId, EstablishmentId = establishmentId, Instant = instant, Horizon = instant.AddDays(15), FromDate = DateOnly.FromDateTime(instant.UtcDateTime).AddDays(-2), ToDate = DateOnly.FromDateTime(instant.UtcDateTime).AddDays(16) };
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var establishment = await grid.ReadSingleOrDefaultAsync<EstablishmentRow>();
        var hours = (await grid.ReadAsync<HoursRow>()).Select(x => BusinessHours.Create(x.TenantId, x.EstablishmentId, (DayOfWeek)x.DayOfWeek, x.OpensAt, x.ClosesAt)).ToArray();
        var exceptions = (await grid.ReadAsync<ExceptionRow>()).Select(x => x.IsOpen
            ? ServiceScheduleException.CreateOpen(x.TenantId, x.EstablishmentId, x.Date, x.ServiceType is null ? null : (OrderServiceType)x.ServiceType, x.OpensAt!.Value, x.ClosesAt!.Value, x.Reason)
            : ServiceScheduleException.CreateClosed(x.TenantId, x.EstablishmentId, x.Date, x.ServiceType is null ? null : (OrderServiceType)x.ServiceType, x.Reason)).ToArray();
        var pauses = (await grid.ReadAsync<PauseRow>()).Select(x => ServicePause.Create(x.TenantId, x.EstablishmentId, (OrderServiceType)x.ServiceType, x.StartsAt, x.EndsAt, x.Reason)).ToArray();
        return AvailabilityEvaluator.Evaluate(establishment?.IsActive == true, establishment?.TimeZoneId ?? "UTC", hours, exceptions, pauses, serviceType, instant);
    }

    private sealed record EstablishmentRow(bool IsActive, string TimeZoneId);
    private sealed record HoursRow
    {
        public Guid TenantId { get; init; }
        public Guid EstablishmentId { get; init; }
        public short DayOfWeek { get; init; }
        public TimeOnly OpensAt { get; init; }
        public TimeOnly ClosesAt { get; init; }
    }
    private sealed record ExceptionRow
    {
        public Guid TenantId { get; init; }
        public Guid EstablishmentId { get; init; }
        public DateOnly Date { get; init; }
        public short? ServiceType { get; init; }
        public bool IsOpen { get; init; }
        public TimeOnly? OpensAt { get; init; }
        public TimeOnly? ClosesAt { get; init; }
        public string? Reason { get; init; }
    }
    private sealed record PauseRow
    {
        public Guid TenantId { get; init; }
        public Guid EstablishmentId { get; init; }
        public short ServiceType { get; init; }
        public DateTimeOffset StartsAt { get; init; }
        public DateTimeOffset? EndsAt { get; init; }
        public string? Reason { get; init; }
    }
}
