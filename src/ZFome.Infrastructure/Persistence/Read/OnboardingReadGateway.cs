using Dapper;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Exceptions;
using ZFome.Application.Onboarding;

namespace ZFome.Infrastructure.Persistence.Read;

public sealed class OnboardingReadGateway(IReadConnectionFactory connections) : IOnboardingReadGateway
{
    public async Task<OnboardingProgress> GetProgressAsync(Guid tenantId, Guid establishmentId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<ProgressRow>(new CommandDefinition("""
            select e.trade_name as TradeName, e.slug as Slug, e.is_active and t.is_active as Active, e.onboarding_completed_at as CompletedAt,
            (select count(*)::int from operations.business_hours h where h.tenant_id=e.tenant_id and h.establishment_id=e.id and h.is_active and h.closes_at<>h.opens_at) as Hours,
            (select count(*)::int from identity.administrative_user u where u.tenant_id=e.tenant_id and u.is_active
              and exists(select 1 from identity.administrative_user_role r where r.user_id=u.id and r.role_id in (1,2))
              and exists(select 1 from identity.user_establishment_access a where a.tenant_id=e.tenant_id and a.user_id=u.id and a.establishment_id=e.id and a.is_active)) as Administrators,
            (select count(*)::int from operations.service_table s where s.tenant_id=e.tenant_id and s.establishment_id=e.id and s.is_active) as ActiveTables
            from tenancy.establishment e join tenancy.tenant t on t.id=e.tenant_id
            where e.tenant_id=@TenantId and e.id=@EstablishmentId
            """, new { TenantId = tenantId, EstablishmentId = establishmentId }, cancellationToken: ct)) ?? throw new NotFoundException("Establishment was not found.");
        return OnboardingProgress.Calculate(row.TradeName, row.Slug, row.Active, row.Hours, row.Administrators, row.ActiveTables, row.CompletedAt);
    }
    public async Task<ConfigurationReadModel> GetConfigurationAsync(Guid tenantId, Guid establishmentId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var parameters = new { TenantId = tenantId, EstablishmentId = establishmentId };
        var row = await connection.QuerySingleOrDefaultAsync<ConfigurationRow>(new CommandDefinition("""
            select e.trade_name as TradeName, e.slug as Slug, e.time_zone_id as TimeZoneId, t.primary_color as PrimaryColor, t.secondary_color as SecondaryColor,
            t.background_color as BackgroundColor, t.text_color as TextColor, t.font_family as FontFamily, t.logo_url as LogoUrl, t.favicon_url as FaviconUrl
            from tenancy.establishment e join tenancy.establishment_theme t on t.establishment_id=e.id
            where e.tenant_id=@TenantId and e.id=@EstablishmentId
            """, parameters, transaction, cancellationToken: ct)) ?? throw new NotFoundException("Establishment was not found.");
        var hours = await connection.QueryAsync<HoursRow>(new CommandDefinition("""
            select day_of_week as Day, opens_at as OpensAt, closes_at as ClosesAt from operations.business_hours
            where tenant_id=@TenantId and establishment_id=@EstablishmentId and is_active order by day_of_week, opens_at, id
            """, parameters, transaction, cancellationToken: ct));
        var exceptions = await connection.QueryAsync<ExceptionRow>(new CommandDefinition("""
            select date,service_type as ServiceType,is_open as IsOpen,opens_at as OpensAt,closes_at as ClosesAt,reason
            from operations.service_schedule_exception
            where tenant_id=@TenantId and establishment_id=@EstablishmentId order by date,service_type,id
            """, parameters, transaction, cancellationToken: ct));
        var pauses = await connection.QueryAsync<PauseRow>(new CommandDefinition("""
            select service_type as ServiceType,starts_at as StartsAt,ends_at as EndsAt,reason
            from operations.service_pause
            where tenant_id=@TenantId and establishment_id=@EstablishmentId and cancelled_at is null and (ends_at is null or ends_at>now())
            order by service_type,starts_at desc
            """, parameters, transaction, cancellationToken: ct));
        await transaction.CommitAsync(ct);
        return new(row.TradeName, row.Slug, row.TimeZoneId, new(row.PrimaryColor, row.SecondaryColor, row.BackgroundColor, row.TextColor, row.FontFamily, row.LogoUrl, row.FaviconUrl),
            hours.Select(h => new HoursInput((DayOfWeek)h.Day, h.OpensAt, h.ClosesAt)).ToArray(),
            exceptions.Select(x => new ScheduleExceptionReadModel(x.Date, x.ServiceType is null ? null : (ZFome.Domain.Ordering.OrderServiceType)x.ServiceType, x.IsOpen, x.OpensAt, x.ClosesAt, x.Reason)).ToArray(),
            pauses.Select(x => new ServicePauseReadModel((ZFome.Domain.Ordering.OrderServiceType)x.ServiceType, x.StartsAt, x.EndsAt, x.Reason)).ToArray());
    }
    public async Task<TableSearchResult> SearchTablesAsync(Guid tenantId, SearchTablesQuery query, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var parameters = new { TenantId = tenantId, query.EstablishmentId, query.PageSize, Offset = (query.Page - 1) * query.PageSize };
        var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition("select count(*) from operations.service_table where tenant_id=@TenantId and establishment_id=@EstablishmentId", parameters, transaction, cancellationToken: ct));
        var rows = await connection.QueryAsync<TableRow>(new CommandDefinition("""
            select s.id as Id, s.code as Code, s.description as Description, s.is_active as IsActive, s.qr_code_token as Token, e.slug as Slug
            from operations.service_table s join tenancy.establishment e on e.tenant_id=s.tenant_id and e.id=s.establishment_id
            where s.tenant_id=@TenantId and s.establishment_id=@EstablishmentId order by s.code, s.id limit @PageSize offset @Offset
            """, parameters, transaction, cancellationToken: ct));
        await transaction.CommitAsync(ct);
        return new(rows.Select(r => new TableReadModel(r.Id, r.Code, r.Description, r.IsActive,
            r.IsActive ? $"/order/{Uri.EscapeDataString(r.Slug)}/table/{Uri.EscapeDataString(r.Token)}" : null)).ToArray(), count, query.Page, query.PageSize);
    }
    private sealed record ProgressRow
    {
        public string TradeName { get; init; } = ""; public string Slug { get; init; } = ""; public bool Active { get; init; }
        public int Hours { get; init; } public int Administrators { get; init; } public int ActiveTables { get; init; } public DateTimeOffset? CompletedAt { get; init; }
    }
    private sealed record ConfigurationRow
    {
        public string TradeName { get; init; } = ""; public string Slug { get; init; } = ""; public string TimeZoneId { get; init; } = "UTC";
        public string? PrimaryColor { get; init; } public string? SecondaryColor { get; init; } public string? BackgroundColor { get; init; }
        public string? TextColor { get; init; } public string? FontFamily { get; init; } public string? LogoUrl { get; init; } public string? FaviconUrl { get; init; }
    }
    private sealed record HoursRow { public short Day { get; init; } public TimeOnly OpensAt { get; init; } public TimeOnly ClosesAt { get; init; } }
    private sealed record ExceptionRow { public DateOnly Date { get; init; } public short? ServiceType { get; init; } public bool IsOpen { get; init; } public TimeOnly? OpensAt { get; init; } public TimeOnly? ClosesAt { get; init; } public string? Reason { get; init; } }
    private sealed record PauseRow { public short ServiceType { get; init; } public DateTimeOffset StartsAt { get; init; } public DateTimeOffset? EndsAt { get; init; } public string? Reason { get; init; } }
    private sealed record TableRow
    {
        public Guid Id { get; init; } public string Code { get; init; } = ""; public string? Description { get; init; }
        public bool IsActive { get; init; } public string Token { get; init; } = ""; public string Slug { get; init; } = "";
    }
}
