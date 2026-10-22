using Dapper;
using System.Text.Json;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Domain.Ordering;

namespace ZFome.Infrastructure.Persistence.Read;

/// <summary>
/// Representa um gateway de leitura para pedidos (orders) que fornece métodos para pesquisar e obter informações detalhadas sobre pedidos.
/// </summary>
public sealed class OrderReadGateway(IReadConnectionFactory connectionFactory, TimeProvider timeProvider) : IOrderReadGateway
{
    public async Task<OrderSearchResult> SearchAsync(Guid tenantId, Guid establishmentId, DateTimeOffset? from, DateTimeOffset? to, OrderStatus? status, long? number, OrderServiceType? serviceType, int page, int pageSize, CancellationToken cancellationToken)
    {
        const string sql = """
            select count(*) from orders."order" o
            where o.tenant_id=@TenantId and o.establishment_id=@EstablishmentId
              and (@From is null or o.created_at>=@From) and (@To is null or o.created_at<@To)
              and (@Status is null or o.status=@Status) and (@Number is null or o.number=@Number)
              and (@ServiceType is null or o.service_type=@ServiceType);
            select o.id,o.number,o.service_type as ServiceType,o.status,o.customer_name as CustomerName,o.customer_phone as CustomerPhone,o.total,o.created_at as CreatedAt,
                   o.scheduled_at_utc as ScheduledAtUtc,o.scheduled_time_zone_id as ScheduledTimeZoneId,
                   case when o.scheduled_at_utc is null then true else o.scheduled_at_utc <= @Now + (coalesce(scheduling.minimum_advance_minutes, 60) * interval '1 minute') end as PreparationAvailable,
                   coalesce((
                       select jsonb_agg(jsonb_build_object(
                           'Quantity', i.quantity,
                           'ProductName', i.product_name,
                           'VariationName', i.variation_name
                       ) order by i.id)
                       from orders.order_item i
                       where i.tenant_id=o.tenant_id
                         and i.establishment_id=o.establishment_id
                         and i.order_id=o.id
                   ), '[]'::jsonb)::text as ItemsJson
            from orders."order" o
            left join operations.order_scheduling_policy scheduling
              on scheduling.tenant_id=o.tenant_id and scheduling.establishment_id=o.establishment_id
             and scheduling.service_type = case o.service_type when 'Pickup' then 1 when 'Delivery' then 2 end
            where o.tenant_id=@TenantId and o.establishment_id=@EstablishmentId
              and (@From is null or o.created_at>=@From) and (@To is null or o.created_at<@To)
              and (@Status is null or o.status=@Status) and (@Number is null or o.number=@Number)
              and (@ServiceType is null or o.service_type=@ServiceType)
            order by (o.scheduled_at_utc is not null),o.scheduled_at_utc asc nulls first,o.created_at desc,o.id desc offset @Offset rows fetch next @PageSize rows only;
            """;
        var parameters = new { TenantId = tenantId, EstablishmentId = establishmentId, From = from, To = to, Status = status?.ToString(), Number = number, ServiceType = serviceType?.ToString(), Now = timeProvider.GetUtcNow().UtcDateTime, Offset = (page - 1) * pageSize, PageSize = pageSize };
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken); using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var total = await grid.ReadSingleAsync<int>(); var rows = (await grid.ReadAsync<SummaryRow>()).ToArray();
        return new(total, rows.Select(x => new OrderSummaryReadModel(x.Id, x.Number, Enum.Parse<OrderServiceType>(x.ServiceType), Enum.Parse<OrderStatus>(x.Status), x.CustomerName, x.CustomerPhone, x.Total, new DateTimeOffset(DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc)), ToUtcOffset(x.ScheduledAtUtc), x.ScheduledTimeZoneId, JsonSerializer.Deserialize<SummaryItemRow[]>(x.ItemsJson)?.Select(item => new OrderSummaryItemReadModel(item.Quantity, item.ProductName, item.VariationName)).ToArray() ?? [], x.PreparationAvailable)).ToArray());
    }

    /// <summary>
    /// Obtém informações detalhadas de um pedido específico com base no ID do inquilino, ID do estabelecimento e ID do pedido.
    /// </summary>
    /// <returns></returns>
    public async Task<OrderReadModel?> GetAsync(Guid tenantId, Guid establishmentId, Guid orderId, CancellationToken cancellationToken)
    {
        const string sql = """
            select o.id, o.number, o.public_reference as PublicReference, o.service_type as ServiceType, o.status,
                   o.customer_name as CustomerName, o.customer_phone as CustomerPhone, t.code as TableCode,
                   o.delivery_street as DeliveryStreet, o.delivery_number as DeliveryNumber, o.delivery_complement as DeliveryComplement,
                   o.delivery_neighborhood as DeliveryNeighborhood, o.delivery_city as DeliveryCity, o.delivery_state as DeliveryState,
                   o.delivery_postal_code as DeliveryPostalCode, o.delivery_region_id as DeliveryRegionId,
                   o.delivery_region_name as DeliveryRegionName, o.delivery_fee as DeliveryFee,
                   o.delivery_estimated_minutes as DeliveryEstimatedMinutes, o.scheduled_at_utc as ScheduledAtUtc,
                   case when o.scheduled_at_utc is null then true else o.scheduled_at_utc <= @Now + (coalesce(scheduling.minimum_advance_minutes, 60) * interval '1 minute') end as PreparationAvailable,
                   o.scheduled_time_zone_id as ScheduledTimeZoneId, o.subtotal, o.discount, o.fees, o.total,
                   o.coupon_code as CouponCode, case when o.coupon_code is null then 0 else o.discount end as CouponDiscount,
                   coalesce((
                       select sum(p.amount)
                       from payments.payment p
                       where p.tenant_id = o.tenant_id
                         and p.establishment_id = o.establishment_id
                         and p.order_id = o.id
                         and p.status = 'Confirmed'
                   ), 0) as ConfirmedAmount
            from orders."order" o
            left join operations.order_scheduling_policy scheduling
              on scheduling.tenant_id=o.tenant_id and scheduling.establishment_id=o.establishment_id
             and scheduling.service_type = case o.service_type when 'Pickup' then 1 when 'Delivery' then 2 end
            left join operations.service_table t on t.tenant_id = o.tenant_id and t.establishment_id = o.establishment_id and t.id = o.table_id
            where o.tenant_id = @TenantId and o.establishment_id = @EstablishmentId and o.id = @OrderId;

            select i.id, i.product_name as ProductName, i.variation_name as VariationName, i.unit_price as UnitPrice, i.base_price as BasePrice,
                   i.quantity, i.total, i.notes
            from orders.order_item i
            where i.tenant_id = @TenantId and i.establishment_id = @EstablishmentId and i.order_id = @OrderId
            order by i.id;

            select a.order_item_id as OrderItemId, a.name, a.unit_price as UnitPrice, a.quantity
            from orders.order_item_additional a
            join orders.order_item i on i.tenant_id = a.tenant_id and i.establishment_id = a.establishment_id and i.id = a.order_item_id
            where a.tenant_id = @TenantId and a.establishment_id = @EstablishmentId and i.order_id = @OrderId
            order by a.id;

            select g.id as SnapshotId,g.order_item_id as OrderItemId,g.modifier_group_id as GroupId,g.name,g.pricing_strategy as PricingStrategy,g.price
            from orders.order_item_modifier_group g
            join orders.order_item i on i.tenant_id=g.tenant_id and i.establishment_id=g.establishment_id and i.id=g.order_item_id
            where g.tenant_id=@TenantId and g.establishment_id=@EstablishmentId and i.order_id=@OrderId
            order by g.id;

            select o.order_item_modifier_group_id as SnapshotId,o.modifier_option_id as OptionId,o.name,o.unit_price as UnitPrice,o.quantity,o.portion_numerator as PortionNumerator,o.portion_denominator as PortionDenominator
            from orders.order_item_modifier_option o
            join orders.order_item_modifier_group g on g.tenant_id=o.tenant_id and g.establishment_id=o.establishment_id and g.id=o.order_item_modifier_group_id
            join orders.order_item i on i.tenant_id=g.tenant_id and i.establishment_id=g.establishment_id and i.id=g.order_item_id
            where o.tenant_id=@TenantId and o.establishment_id=@EstablishmentId and i.order_id=@OrderId
            order by o.id;

            select h.previous_status as PreviousStatus, h.new_status as NewStatus, h.occurred_at as OccurredAt, h.actor_id as ActorId, h.note
            from orders.order_status_history h
            where h.tenant_id = @TenantId and h.establishment_id = @EstablishmentId and h.order_id = @OrderId
            order by h.occurred_at, h.id;
            """;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, EstablishmentId = establishmentId, OrderId = orderId, Now = timeProvider.GetUtcNow().UtcDateTime }, cancellationToken: cancellationToken));
        var order = await grid.ReadSingleOrDefaultAsync<OrderRow>(); if (order is null) return null;
        var items = (await grid.ReadAsync<ItemRow>()).ToArray(); var additionals = (await grid.ReadAsync<AdditionalRow>()).ToLookup(x => x.OrderItemId);
        var modifierGroups = (await grid.ReadAsync<ModifierGroupRow>()).ToArray(); var modifierOptions = (await grid.ReadAsync<ModifierOptionRow>()).ToLookup(x => x.SnapshotId);
        var history = (await grid.ReadAsync<HistoryRow>()).ToArray();
        var address = order.DeliveryStreet is null ? null : new DeliveryAddressSnapshot(order.DeliveryStreet, order.DeliveryNumber!, order.DeliveryComplement, order.DeliveryNeighborhood!, order.DeliveryCity!, order.DeliveryState!, order.DeliveryPostalCode!);
        return new(order.Id, order.Number, order.PublicReference, Enum.Parse<OrderServiceType>(order.ServiceType), Enum.Parse<OrderStatus>(order.Status), order.CustomerName, order.CustomerPhone, order.TableCode, address,
            order.Subtotal, order.Discount, order.Fees, order.Total, order.CouponCode, order.CouponDiscount,
            order.ConfirmedAmount, order.ConfirmedAmount >= order.Total,
            items.Select(item => new OrderItemReadModel(item.Id, item.ProductName, item.VariationName, item.UnitPrice, item.Quantity, item.Total, item.Notes, additionals[item.Id].Select(a => new OrderAdditionalReadModel(a.Name, a.UnitPrice, a.Quantity)).ToArray(), item.BasePrice,
                modifierGroups.Where(g => g.OrderItemId == item.Id).Select(g => new OrderModifierGroupReadModel(g.GroupId, g.Name, g.PricingStrategy, g.Price, modifierOptions[g.SnapshotId].Select(o => new OrderModifierOptionReadModel(o.OptionId, o.Name, o.UnitPrice, o.Quantity, o.PortionNumerator, o.PortionDenominator)).ToArray())).ToArray())).ToArray(),
            history.Select(item => new OrderHistoryReadModel(Enum.Parse<OrderStatus>(item.PreviousStatus), Enum.Parse<OrderStatus>(item.NewStatus), item.OccurredAt, item.ActorId, item.Note)).ToArray(),
            order.DeliveryRegionId, order.DeliveryRegionName, order.DeliveryFee, order.DeliveryEstimatedMinutes,
            ToUtcOffset(order.ScheduledAtUtc), order.ScheduledTimeZoneId, order.PreparationAvailable);
    }

    private static DateTimeOffset? ToUtcOffset(DateTime? value) => value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private sealed record OrderRow(Guid Id, long? Number, string? PublicReference, string ServiceType, string Status, string? CustomerName, string? CustomerPhone, string? TableCode, string? DeliveryStreet, string? DeliveryNumber, string? DeliveryComplement, string? DeliveryNeighborhood, string? DeliveryCity, string? DeliveryState, string? DeliveryPostalCode, Guid? DeliveryRegionId, string? DeliveryRegionName, decimal DeliveryFee, int? DeliveryEstimatedMinutes, DateTime? ScheduledAtUtc, bool PreparationAvailable, string? ScheduledTimeZoneId, decimal Subtotal, decimal Discount, decimal Fees, decimal Total, string? CouponCode, decimal CouponDiscount, decimal ConfirmedAmount);
    private sealed record ItemRow(Guid Id, string ProductName, string? VariationName, decimal UnitPrice, decimal BasePrice, decimal Quantity, decimal Total, string? Notes);
    private sealed record AdditionalRow(Guid OrderItemId, string Name, decimal UnitPrice, decimal Quantity);
    private sealed record ModifierGroupRow(Guid SnapshotId, Guid OrderItemId, Guid GroupId, string Name, string PricingStrategy, decimal Price);
    private sealed record ModifierOptionRow(Guid SnapshotId, Guid OptionId, string Name, decimal UnitPrice, decimal Quantity, int? PortionNumerator, int? PortionDenominator);

    private sealed class SummaryRow
    { public Guid Id { get; set; } public long Number { get; set; } public string ServiceType { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public string? CustomerName { get; set; } public string? CustomerPhone { get; set; } public decimal Total { get; set; } public DateTime CreatedAt { get; set; } public DateTime? ScheduledAtUtc { get; set; } public string? ScheduledTimeZoneId { get; set; } public bool PreparationAvailable { get; set; } = true; public string ItemsJson { get; set; } = "[]"; }
    private sealed record SummaryItemRow(decimal Quantity, string ProductName, string? VariationName);

    private sealed class HistoryRow
    {
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public DateTimeOffset OccurredAt { get; set; }
        public Guid? ActorId { get; set; }
        public string? Note { get; set; }
    }
}
