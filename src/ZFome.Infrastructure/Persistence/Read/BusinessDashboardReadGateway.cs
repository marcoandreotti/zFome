using Dapper;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Abstractions.Reporting;

namespace ZFome.Infrastructure.Persistence.Read;

public sealed class BusinessDashboardReadGateway(IReadConnectionFactory connectionFactory)
    : IBusinessDashboardReadGateway
{
    public async Task<BusinessDashboardReadModel> GetAsync(
        Guid tenantId,
        Guid establishmentId,
        DateOnly from,
        DateOnly to,
        string? serviceType,
        int productPage,
        int productPageSize,
        CancellationToken cancellationToken)
    {
        var dayCount = to.DayNumber - from.DayNumber + 1;
        var previousFrom = from.AddDays(-dayCount);
        var previousTo = from.AddDays(-1);
        var parameters = new
        {
            TenantId = tenantId,
            EstablishmentId = establishmentId,
            FromDate = from.ToDateTime(TimeOnly.MinValue),
            ToDate = to.ToDateTime(TimeOnly.MinValue),
            PreviousFromDate = previousFrom.ToDateTime(TimeOnly.MinValue),
            PreviousToDate = previousTo.ToDateTime(TimeOnly.MinValue),
            ServiceType = serviceType,
            Offset = (productPage - 1) * productPageSize,
            ProductPageSize = productPageSize
        };

        const string timeZoneSql = """
            select e.time_zone_id
            from tenancy.establishment e
            join tenancy.tenant t on t.id = e.tenant_id
            where e.tenant_id = @TenantId and e.id = @EstablishmentId
              and e.is_active and t.is_active;
            """;
        const string summariesSql = """
            with bounds as (
                select 'current'::text as period, @FromDate::date as local_from, @ToDate::date as local_to,
                       @FromDate::date::timestamp at time zone @TimeZoneId as from_utc,
                       (@ToDate::date + 1)::timestamp at time zone @TimeZoneId as to_utc
                union all
                select 'previous', @PreviousFromDate::date, @PreviousToDate::date,
                       @PreviousFromDate::date::timestamp at time zone @TimeZoneId,
                       (@PreviousToDate::date + 1)::timestamp at time zone @TimeZoneId
            ), received as (
                select b.period, count(distinct h.order_id)::int as total
                from bounds b
                join orders.order_status_history h on h.tenant_id = @TenantId
                    and h.establishment_id = @EstablishmentId
                    and h.new_status = 'Confirmed' and h.occurred_at >= b.from_utc and h.occurred_at < b.to_utc
                join orders."order" o on o.tenant_id = h.tenant_id and o.establishment_id = h.establishment_id and o.id = h.order_id
                where (@ServiceType is null or o.service_type = @ServiceType)
                group by b.period
            ), completed as (
                select b.period, h.order_id, h.occurred_at, o.total
                from bounds b
                join orders.order_status_history h on h.tenant_id = @TenantId
                    and h.establishment_id = @EstablishmentId
                    and h.new_status = 'Completed' and h.occurred_at >= b.from_utc and h.occurred_at < b.to_utc
                join orders."order" o on o.tenant_id = h.tenant_id and o.establishment_id = h.establishment_id and o.id = h.order_id
                where (@ServiceType is null or o.service_type = @ServiceType)
            ), sales as (
                select period, count(distinct order_id)::int as total_orders, coalesce(sum(total), 0)::numeric as revenue
                from completed group by period
            ), terminal as (
                select b.period,
                    count(distinct h.order_id) filter (where h.new_status = 'Cancelled')::int as cancelled,
                    count(distinct h.order_id) filter (where h.new_status = 'Rejected')::int as rejected
                from bounds b
                join orders.order_status_history h on h.tenant_id = @TenantId
                    and h.establishment_id = @EstablishmentId
                    and h.new_status in ('Cancelled', 'Rejected') and h.occurred_at >= b.from_utc and h.occurred_at < b.to_utc
                join orders."order" o on o.tenant_id = h.tenant_id and o.establishment_id = h.establishment_id and o.id = h.order_id
                where (@ServiceType is null or o.service_type = @ServiceType)
                group by b.period
            ), payments as (
                select b.period, coalesce(sum(p.amount), 0)::numeric as amount
                from bounds b
                join payments.payment p on p.tenant_id = @TenantId and p.establishment_id = @EstablishmentId
                    and p.status = 'Confirmed' and p.confirmed_at >= b.from_utc and p.confirmed_at < b.to_utc
                join orders."order" o on o.tenant_id = p.tenant_id and o.establishment_id = p.establishment_id and o.id = p.order_id
                where (@ServiceType is null or o.service_type = @ServiceType)
                group by b.period
            )
            select b.period, b.local_from as "From", b.local_to as "To",
                   coalesce(s.revenue, 0)::numeric as "Revenue",
                   coalesce(r.total, 0)::int as "ReceivedOrders",
                   coalesce(s.total_orders, 0)::int as "CompletedOrders",
                   case when coalesce(s.total_orders, 0) = 0 then 0 else s.revenue / s.total_orders end::numeric as "AverageTicket",
                   coalesce(p.amount, 0)::numeric as "ConfirmedPayments",
                   coalesce(t.cancelled, 0)::int as "CancelledOrders",
                   coalesce(t.rejected, 0)::int as "RejectedOrders"
            from bounds b
            left join received r on r.period = b.period
            left join sales s on s.period = b.period
            left join terminal t on t.period = b.period
            left join payments p on p.period = b.period
            order by b.period desc;
            """;
        const string seriesSql = """
            with bounds as (
                select 'current'::text as period, @FromDate::date as local_from, @ToDate::date as local_to,
                       @FromDate::date::timestamp at time zone @TimeZoneId as from_utc,
                       (@ToDate::date + 1)::timestamp at time zone @TimeZoneId as to_utc
                union all
                select 'previous', @PreviousFromDate::date, @PreviousToDate::date,
                       @PreviousFromDate::date::timestamp at time zone @TimeZoneId,
                       (@PreviousToDate::date + 1)::timestamp at time zone @TimeZoneId
            ), days as (
                select b.period, b.local_from, d::date as local_date, b.from_utc, b.to_utc
                from bounds b cross join lateral generate_series(b.local_from::timestamp, b.local_to::timestamp, interval '1 day') d
            ), completed as (
                select b.period, (h.occurred_at at time zone @TimeZoneId)::date as local_date,
                       count(distinct h.order_id)::int as orders, coalesce(sum(o.total), 0)::numeric as revenue
                from bounds b
                join orders.order_status_history h on h.tenant_id = @TenantId and h.establishment_id = @EstablishmentId
                    and h.new_status = 'Completed' and h.occurred_at >= b.from_utc and h.occurred_at < b.to_utc
                join orders."order" o on o.tenant_id = h.tenant_id and o.establishment_id = h.establishment_id and o.id = h.order_id
                where (@ServiceType is null or o.service_type = @ServiceType)
                group by b.period, (h.occurred_at at time zone @TimeZoneId)::date
            ), collected as (
                select b.period, (p.confirmed_at at time zone @TimeZoneId)::date as local_date,
                       coalesce(sum(p.amount), 0)::numeric as amount
                from bounds b
                join payments.payment p on p.tenant_id = @TenantId and p.establishment_id = @EstablishmentId
                    and p.status = 'Confirmed' and p.confirmed_at >= b.from_utc and p.confirmed_at < b.to_utc
                join orders."order" o on o.tenant_id = p.tenant_id and o.establishment_id = p.establishment_id and o.id = p.order_id
                where (@ServiceType is null or o.service_type = @ServiceType)
                group by b.period, (p.confirmed_at at time zone @TimeZoneId)::date
            )
            select d.local_date as "Date", (d.local_date - d.local_from)::int as "PeriodOffset",
                   (d.period = 'previous') as "IsComparison", coalesce(c.revenue, 0)::numeric as "Revenue",
                   coalesce(c.orders, 0)::int as "CompletedOrders", coalesce(p.amount, 0)::numeric as "ConfirmedPayments"
            from days d
            left join completed c on c.period = d.period and c.local_date = d.local_date
            left join collected p on p.period = d.period and p.local_date = d.local_date
            order by d.period, d.local_date;
            """;
        const string productsCte = """
            with bounds as (
                select @FromDate::date::timestamp at time zone @TimeZoneId as from_utc,
                       (@ToDate::date + 1)::timestamp at time zone @TimeZoneId as to_utc
            ), completed_orders as (
                select distinct h.order_id
                from orders.order_status_history h cross join bounds b
                join orders."order" o on o.tenant_id = h.tenant_id and o.establishment_id = h.establishment_id and o.id = h.order_id
                where h.tenant_id = @TenantId and h.establishment_id = @EstablishmentId
                  and h.new_status = 'Completed' and h.occurred_at >= b.from_utc and h.occurred_at < b.to_utc
                  and (@ServiceType is null or o.service_type = @ServiceType)
            ), product_totals as (
                select i.product_name, sum(i.quantity)::numeric as quantity_sold, sum(i.total)::numeric as revenue
                from completed_orders c
                join orders.order_item i on i.tenant_id = @TenantId and i.establishment_id = @EstablishmentId and i.order_id = c.order_id
                group by i.product_name
            )
            """;
        var productsSql = productsCte + """
            select count(*)::int from product_totals;
            """ + productsCte + """
            select product_name as "Name", quantity_sold as "QuantitySold", revenue as "Revenue"
            from product_totals order by quantity_sold desc, product_name
            offset @Offset rows fetch next @ProductPageSize rows only;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var timeZoneId = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(timeZoneSql, parameters, cancellationToken: cancellationToken));
        if (timeZoneId is null)
            throw new InvalidOperationException("The authorized establishment is unavailable for reporting.");

        var reportParameters = new
        {
            parameters.TenantId,
            parameters.EstablishmentId,
            parameters.FromDate,
            parameters.ToDate,
            parameters.PreviousFromDate,
            parameters.PreviousToDate,
            parameters.ServiceType,
            parameters.Offset,
            parameters.ProductPageSize,
            TimeZoneId = timeZoneId
        };

        using var summaryGrid = await connection.QueryMultipleAsync(
            new CommandDefinition(summariesSql, reportParameters, cancellationToken: cancellationToken));
        var summaries = (await summaryGrid.ReadAsync<PeriodRow>()).ToDictionary(x => x.Period);
        var current = summaries["current"].ToModel();
        var previous = summaries["previous"].ToModel();

        var series = (await connection.QueryAsync<SeriesRow>(
            new CommandDefinition(seriesSql, reportParameters, cancellationToken: cancellationToken)))
            .Select(x => x.ToModel())
            .ToArray();

        using var productsGrid = await connection.QueryMultipleAsync(
            new CommandDefinition(productsSql, reportParameters, cancellationToken: cancellationToken));
        var productCount = await productsGrid.ReadSingleAsync<int>();
        var products = (await productsGrid.ReadAsync<ProductRow>()).ToArray();

        return new BusinessDashboardReadModel(
            timeZoneId,
            current,
            previous,
            series,
            new BusinessDashboardProductPageReadModel(
                productCount,
                productPage,
                productPageSize,
                products.Select(x => new BusinessDashboardProductReadModel(x.Name, x.QuantitySold, x.Revenue)).ToArray()));
    }

    private sealed record PeriodRow(
        string Period,
        DateOnly From,
        DateOnly To,
        decimal Revenue,
        int ReceivedOrders,
        int CompletedOrders,
        decimal AverageTicket,
        decimal ConfirmedPayments,
        int CancelledOrders,
        int RejectedOrders)
    {
        public BusinessDashboardPeriodReadModel ToModel() => new(
            From, To, Revenue, ReceivedOrders, CompletedOrders, AverageTicket,
            ConfirmedPayments, CancelledOrders, RejectedOrders);
    }

    private sealed record SeriesRow(
        DateOnly Date,
        int PeriodOffset,
        bool IsComparison,
        decimal Revenue,
        int CompletedOrders,
        decimal ConfirmedPayments)
    {
        public BusinessDashboardDayReadModel ToModel() => new(
            Date, PeriodOffset, IsComparison, Revenue, CompletedOrders, ConfirmedPayments);
    }

    private sealed record ProductRow(string Name, decimal QuantitySold, decimal Revenue);
}
