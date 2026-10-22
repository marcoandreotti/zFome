using Dapper;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Domain.Operations;
using ZFome.Domain.Ordering;

namespace ZFome.Infrastructure.Persistence.Read;

public sealed class OrderSchedulingReadGateway(IReadConnectionFactory connectionFactory) : IOrderSchedulingReadGateway
{
    public async Task<OrderSchedulingConfigurationReadModel> GetConfigurationAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken)
    {
        const string sql = """
            select e.time_zone_id as TimeZoneId
            from tenancy.establishment e join tenancy.tenant t on t.id = e.tenant_id
            where e.tenant_id = @TenantId and e.id = @EstablishmentId and e.is_active and t.is_active;
            select service_type as ServiceType, is_enabled as IsEnabled, minimum_advance_minutes as MinimumAdvanceMinutes,
                   horizon_days as HorizonDays, maximum_orders_per_slot as MaximumOrdersPerSlot
            from operations.order_scheduling_policy
            where tenant_id = @TenantId and establishment_id = @EstablishmentId;
            """;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql,
            new { TenantId = tenantId, EstablishmentId = establishmentId }, cancellationToken: cancellationToken));
        var timeZoneId = await grid.ReadSingleOrDefaultAsync<string>() ?? "UTC";
        var configured = (await grid.ReadAsync<PolicyRow>()).ToDictionary(x => (OrderServiceType)x.ServiceType);
        var policies = new[] { OrderServiceType.Pickup, OrderServiceType.Delivery }
            .Select(serviceType => configured.TryGetValue(serviceType, out var row)
                ? new OrderSchedulingPolicyReadModel(serviceType, row.IsEnabled, OrderSchedulingPolicy.DefaultSlotIntervalMinutes,
                    row.MinimumAdvanceMinutes, row.HorizonDays, row.MaximumOrdersPerSlot)
                : new OrderSchedulingPolicyReadModel(serviceType, false, OrderSchedulingPolicy.DefaultSlotIntervalMinutes,
                    OrderSchedulingPolicy.DefaultMinimumAdvanceMinutes, OrderSchedulingPolicy.DefaultHorizonDays, null))
            .ToArray();
        return new(timeZoneId, policies);
    }

    public async Task<OrderSchedulingSlotsReadModel> GetPublicSlotsAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string sql = """
            select e.is_active and t.is_active as IsActive, e.time_zone_id as TimeZoneId
            from tenancy.establishment e join tenancy.tenant t on t.id = e.tenant_id
            where e.tenant_id = @TenantId and e.id = @EstablishmentId;
            select is_enabled as IsEnabled, minimum_advance_minutes as MinimumAdvanceMinutes, horizon_days as HorizonDays,
                   maximum_orders_per_slot as MaximumOrdersPerSlot
            from operations.order_scheduling_policy
            where tenant_id = @TenantId and establishment_id = @EstablishmentId and service_type = @ServiceType;
            select tenant_id as TenantId, establishment_id as EstablishmentId, day_of_week as DayOfWeek,
                   opens_at as OpensAt, closes_at as ClosesAt
            from operations.business_hours
            where tenant_id = @TenantId and establishment_id = @EstablishmentId and is_active;
            select tenant_id as TenantId, establishment_id as EstablishmentId, date, service_type as ServiceType,
                   is_open as IsOpen, opens_at as OpensAt, closes_at as ClosesAt, reason
            from operations.service_schedule_exception
            where tenant_id = @TenantId and establishment_id = @EstablishmentId and date between @FromDate and @ToDate;
            select tenant_id as TenantId, establishment_id as EstablishmentId, service_type as ServiceType,
                   starts_at as StartsAt, ends_at as EndsAt, reason
            from operations.service_pause
            where tenant_id = @TenantId and establishment_id = @EstablishmentId and service_type = @ServiceType
              and cancelled_at is null and starts_at <= @Latest and (ends_at is null or ends_at > @Now);
            select scheduled_at_utc as StartsAt, count(*)::int as Reserved
            from orders."order"
            where tenant_id = @TenantId and establishment_id = @EstablishmentId and service_type = @ServiceTypeName
              and scheduled_at_utc between @Earliest and @Latest and status not in ('Cancelled', 'Rejected')
            group by scheduled_at_utc;
            """;

        var latest = now.AddDays(OrderSchedulingPolicy.MaximumHorizonDays);
        var parameters = new
        {
            TenantId = tenantId,
            EstablishmentId = establishmentId,
            ServiceType = (short)serviceType,
            ServiceTypeName = serviceType.ToString(),
            Now = now,
            Earliest = now,
            Latest = latest,
            FromDate = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-2),
            ToDate = DateOnly.FromDateTime(latest.UtcDateTime).AddDays(2)
        };
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var establishment = await grid.ReadSingleOrDefaultAsync<EstablishmentRow>();
        var policyRow = await grid.ReadSingleOrDefaultAsync<PolicyDetailsRow>();
        var policy = OrderSchedulingPolicy.Create(tenantId, establishmentId, serviceType,
            establishment?.IsActive == true && policyRow?.IsEnabled == true,
            policyRow?.MinimumAdvanceMinutes ?? OrderSchedulingPolicy.DefaultMinimumAdvanceMinutes,
            policyRow?.HorizonDays ?? OrderSchedulingPolicy.DefaultHorizonDays,
            policyRow?.MaximumOrdersPerSlot);
        var hours = (await grid.ReadAsync<HoursRow>())
            .Select(x => BusinessHours.Create(x.TenantId, x.EstablishmentId, (DayOfWeek)x.DayOfWeek, x.OpensAt, x.ClosesAt)).ToArray();
        var exceptions = (await grid.ReadAsync<ExceptionRow>()).Select(x => x.IsOpen
            ? ServiceScheduleException.CreateOpen(x.TenantId, x.EstablishmentId, x.Date,
                x.ServiceType is null ? null : (OrderServiceType)x.ServiceType, x.OpensAt!.Value, x.ClosesAt!.Value, x.Reason)
            : ServiceScheduleException.CreateClosed(x.TenantId, x.EstablishmentId, x.Date,
                x.ServiceType is null ? null : (OrderServiceType)x.ServiceType, x.Reason)).ToArray();
        var pauses = (await grid.ReadAsync<PauseRow>())
            .Select(x => ServicePause.Create(x.TenantId, x.EstablishmentId, (OrderServiceType)x.ServiceType,
                ToUtcOffset(x.StartsAt), x.EndsAt is { } endsAt ? ToUtcOffset(endsAt) : null, x.Reason)).ToArray();
        var reserved = (await grid.ReadAsync<CapacityRow>())
            .ToDictionary(x => ToUtcOffset(x.StartsAt), x => x.Reserved);
        var zone = establishment?.TimeZoneId ?? "UTC";
        var slots = OrderSchedulingEvaluator.GetSlots(policy, establishment?.IsActive == true, zone, hours, exceptions, pauses, reserved, now);
        return new(serviceType, policy.IsEnabled, zone, policy.SlotIntervalMinutes, policy.MinimumAdvanceMinutes, policy.HorizonDays, slots);
    }

    private sealed record EstablishmentRow(bool IsActive, string TimeZoneId);
    private sealed record PolicyRow(short ServiceType, bool IsEnabled, int MinimumAdvanceMinutes, int HorizonDays, int? MaximumOrdersPerSlot);
    private sealed record PolicyDetailsRow(bool IsEnabled, int MinimumAdvanceMinutes, int HorizonDays, int? MaximumOrdersPerSlot);
    private sealed record CapacityRow(DateTime StartsAt, int Reserved);
    private sealed record HoursRow(Guid TenantId, Guid EstablishmentId, short DayOfWeek, TimeOnly OpensAt, TimeOnly ClosesAt);
    private sealed record ExceptionRow(Guid TenantId, Guid EstablishmentId, DateOnly Date, short? ServiceType, bool IsOpen, TimeOnly? OpensAt, TimeOnly? ClosesAt, string? Reason);
    private sealed record PauseRow(Guid TenantId, Guid EstablishmentId, short ServiceType, DateTime StartsAt, DateTime? EndsAt, string? Reason);

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
