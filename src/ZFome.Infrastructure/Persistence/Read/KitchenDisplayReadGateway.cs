using Dapper;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Domain.Ordering;

namespace ZFome.Infrastructure.Persistence.Read;

public sealed class KitchenDisplayReadGateway(IReadConnectionFactory connectionFactory)
    : IKitchenDisplayReadGateway
{
    public async Task<IReadOnlyList<KitchenTicketReadModel>> GetQueueAsync(
        Guid tenantId,
        Guid establishmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select o.id,
                   o.number,
                   o.service_type as ServiceType,
                   o.status,
                   o.customer_name as CustomerName,
                   t.code as TableCode,
                   confirmed.occurred_at as ConfirmedAt,
                   preparing.occurred_at as PreparationStartedAt
            from orders."order" o
            left join operations.service_table t
              on t.tenant_id = o.tenant_id
             and t.establishment_id = o.establishment_id
             and t.id = o.table_id
            inner join orders.order_status_history confirmed
              on confirmed.tenant_id = o.tenant_id
             and confirmed.establishment_id = o.establishment_id
             and confirmed.order_id = o.id
             and confirmed.new_status = 'Confirmed'
            left join orders.order_status_history preparing
              on preparing.tenant_id = o.tenant_id
             and preparing.establishment_id = o.establishment_id
             and preparing.order_id = o.id
             and preparing.new_status = 'Preparing'
            where o.tenant_id = @TenantId
              and o.establishment_id = @EstablishmentId
              and o.status in ('Confirmed', 'Preparing')
            order by case when o.status = 'Preparing' then 0 else 1 end,
                     confirmed.occurred_at,
                     o.number;

            select i.id,
                   i.order_id as OrderId,
                   i.product_name as ProductName,
                   i.variation_name as VariationName,
                   i.quantity,
                   i.notes
            from orders.order_item i
            inner join orders."order" o
              on o.tenant_id = i.tenant_id
             and o.establishment_id = i.establishment_id
             and o.id = i.order_id
            where o.tenant_id = @TenantId
              and o.establishment_id = @EstablishmentId
              and o.status in ('Confirmed', 'Preparing')
            order by i.order_id, i.id;

            select a.order_item_id as OrderItemId,
                   a.name,
                   a.quantity
            from orders.order_item_additional a
            inner join orders.order_item i
              on i.tenant_id = a.tenant_id
             and i.establishment_id = a.establishment_id
             and i.id = a.order_item_id
            inner join orders."order" o
              on o.tenant_id = i.tenant_id
             and o.establishment_id = i.establishment_id
             and o.id = i.order_id
            where o.tenant_id = @TenantId
              and o.establishment_id = @EstablishmentId
              and o.status in ('Confirmed', 'Preparing')
            order by a.order_item_id, a.id;
            """;

        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new { TenantId = tenantId, EstablishmentId = establishmentId },
            cancellationToken: cancellationToken));

        var tickets = (await grid.ReadAsync<TicketRow>()).ToArray();
        var items = (await grid.ReadAsync<ItemRow>()).ToArray();
        var additionals = (await grid.ReadAsync<AdditionalRow>()).ToLookup(row => row.OrderItemId);
        var itemsByOrder = items.ToLookup(row => row.OrderId);

        return tickets.Select(ticket => new KitchenTicketReadModel(
            ticket.Id,
            ticket.Number,
            Enum.Parse<OrderServiceType>(ticket.ServiceType),
            Enum.Parse<OrderStatus>(ticket.Status),
            ticket.CustomerName,
            ticket.TableCode,
            AsUtc(ticket.ConfirmedAt),
            ticket.PreparationStartedAt is { } startedAt ? AsUtc(startedAt) : null,
            Enum.Parse<OrderStatus>(ticket.Status) == OrderStatus.Confirmed
                ? KitchenTicketAction.StartPreparation
                : KitchenTicketAction.MarkReady,
            itemsByOrder[ticket.Id].Select(item => new KitchenItemReadModel(
                item.Id,
                item.ProductName,
                item.VariationName,
                item.Quantity,
                item.Notes,
                additionals[item.Id]
                    .Select(value => new KitchenAdditionalReadModel(value.Name, value.Quantity))
                    .ToArray()))
                .ToArray()))
            .ToArray();
    }

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record TicketRow(
        Guid Id,
        long Number,
        string ServiceType,
        string Status,
        string? CustomerName,
        string? TableCode,
        DateTime ConfirmedAt,
        DateTime? PreparationStartedAt);

    private sealed record ItemRow(
        Guid Id,
        Guid OrderId,
        string ProductName,
        string? VariationName,
        decimal Quantity,
        string? Notes);

    private sealed record AdditionalRow(Guid OrderItemId, string Name, decimal Quantity);
}
