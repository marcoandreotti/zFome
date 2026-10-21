using Dapper;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Domain.Ordering;
using ZFome.Domain.SharedKernel;
using ZFome.Domain.Catalog;

namespace ZFome.Infrastructure.Persistence.Read;

/// <summary>
/// Representa um resolvedor de ofertas de pedidos que fornece métodos para resolver informações sobre produtos, variações e adicionais de um pedido com base em critérios específicos.
/// </summary>
public sealed class OrderOfferResolver(IReadConnectionFactory connectionFactory) : IOrderOfferResolver
{
    public async Task<OrderOfferSnapshot?> ResolveAsync(Guid tenantId, Guid establishmentId, Guid productId, Guid? variationId, IReadOnlyCollection<OrderAdditionalSelection> additionals, DateTimeOffset instant, CancellationToken cancellationToken)
    {
        const string offerSql = """
            select p.id as ProductId, p.name as ProductName, v.id as VariationId, v.name as VariationName,
                   coalesce(v.price, p.base_price) as UnitPrice
            from catalog.product p
            left join catalog.product_variation v on v.product_id = p.id and v.id = @VariationId and v.is_active
            where p.tenant_id = @TenantId and p.establishment_id = @EstablishmentId and p.id = @ProductId and p.is_active
              and not exists (
                  select 1 from catalog.offer_unavailability u
                  where u.tenant_id=p.tenant_id and u.establishment_id=p.establishment_id and u.kind=0 and u.offer_id=p.id
                    and u.reactivated_at is null and u.starts_at<=@Instant and (u.ends_at is null or u.ends_at>@Instant))
              and (@VariationId is null or not exists (
                  select 1 from catalog.offer_unavailability u
                  where u.tenant_id=p.tenant_id and u.establishment_id=p.establishment_id and u.kind=1 and u.offer_id=@VariationId
                    and u.reactivated_at is null and u.starts_at<=@Instant and (u.ends_at is null or u.ends_at>@Instant)))
              and ((@VariationId is null and not exists (select 1 from catalog.product_variation pv where pv.product_id = p.id and pv.is_active)) or v.id is not null);
            """;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<OfferRow>(new CommandDefinition(offerSql, new { TenantId = tenantId, EstablishmentId = establishmentId, ProductId = productId, VariationId = variationId, Instant = instant }, cancellationToken: cancellationToken));
        if (row is null) return null;

        if (additionals.Select(x => x.AdditionalId).Distinct().Count() != additionals.Count) return null;
        const string groupsSql = """
            select g.id as GroupId,g.name as GroupName,g.minimum_selection as MinimumSelection,g.maximum_selection as MaximumSelection,
                   g.pricing_strategy as PricingStrategy,g.requires_complete_composition as RequiresCompleteComposition,
                   gi.additional_id as AdditionalId,a.name as OptionName,a.price as UnitPrice,
                   (a.is_active and not exists (select 1 from catalog.offer_unavailability u where u.tenant_id=a.tenant_id and u.establishment_id=a.establishment_id and u.kind=2 and u.offer_id=a.id and u.reactivated_at is null and u.starts_at<=@Instant and (u.ends_at is null or u.ends_at>@Instant))) as IsAvailable
            from catalog.product_additional_group pg
            join catalog.additional_group g on g.tenant_id=pg.tenant_id and g.establishment_id=pg.establishment_id and g.id=pg.group_id and g.is_active
            left join catalog.additional_group_item gi on gi.tenant_id=g.tenant_id and gi.establishment_id=g.establishment_id and gi.group_id=g.id
            left join catalog.additional a on a.tenant_id=gi.tenant_id and a.establishment_id=gi.establishment_id and a.id=gi.additional_id
            where pg.tenant_id=@TenantId and pg.establishment_id=@EstablishmentId and pg.product_id=@ProductId;
            """;
        var groupRows = (await connection.QueryAsync<ModifierRow>(new CommandDefinition(groupsSql, new { TenantId = tenantId, EstablishmentId = establishmentId, ProductId = productId, Instant = instant }, cancellationToken: cancellationToken))).ToArray();
        const string rulesSql = """
            select r.source_group_id as SourceGroupId,r.source_additional_id as SourceOptionId,r.target_group_id as TargetGroupId,
                   r.target_additional_id as TargetOptionId,r.kind as Kind
            from catalog.additional_group_compatibility_rule r
            join catalog.product_additional_group pg on pg.tenant_id=r.tenant_id and pg.establishment_id=r.establishment_id and pg.group_id=r.source_group_id and pg.product_id=@ProductId
            where r.tenant_id=@TenantId and r.establishment_id=@EstablishmentId;
            """;
        var rules = (await connection.QueryAsync<CompatibilityRow>(new CommandDefinition(rulesSql, new { TenantId = tenantId, EstablishmentId = establishmentId, ProductId = productId }, cancellationToken: cancellationToken)))
            .Select(x => new ModifierCompatibilityRuleInput(x.SourceGroupId, x.SourceOptionId, x.TargetGroupId, x.TargetOptionId, Enum.Parse<ModifierCompatibilityKind>(x.Kind)))
            .ToArray();
        var resolvedSelections = additionals.Select(selection =>
        {
            if (selection.GroupId is not null) return selection;
            var matchingGroups = groupRows.Where(x => x.AdditionalId == selection.AdditionalId).Select(x => x.GroupId).Distinct().ToArray();
            return matchingGroups.Length == 1 ? selection with { GroupId = matchingGroups[0] } : selection;
        }).ToArray();
        if (resolvedSelections.Any(x => x.GroupId is null)) return null;
        var selections = new List<ModifierOptionSelectionInput>();
        var compositionGroups = new List<ModifierGroupCompositionInput>();
        foreach (var group in groupRows.GroupBy(x => new { x.GroupId, x.GroupName, x.MinimumSelection, x.MaximumSelection, x.PricingStrategy, x.RequiresCompleteComposition }))
        {
            var strategy = Enum.Parse<ModifierPricingStrategy>(group.Key.PricingStrategy);
            var optionRows = group.Where(x => x.AdditionalId is not null).ToDictionary(x => x.AdditionalId!.Value);
            var chosen = resolvedSelections.Where(x => x.GroupId == group.Key.GroupId).ToArray();
            if (chosen.Any(x => !optionRows.ContainsKey(x.AdditionalId) || !optionRows[x.AdditionalId].IsAvailable)) return null;
            var resolved = chosen.Select(x =>
            {
                var option = optionRows[x.AdditionalId];
                var portion = x.PortionNumerator is { } numerator && x.PortionDenominator is { } denominator
                    ? new ModifierPortion(numerator, denominator)
                    : (ModifierPortion?)null;
                return new ModifierOptionSelectionInput(group.Key.GroupId, x.AdditionalId, option.OptionName!, new Money(option.UnitPrice!.Value), new Quantity(x.Quantity), portion);
            }).ToArray();
            selections.AddRange(resolved);
            compositionGroups.Add(new ModifierGroupCompositionInput(group.Key.GroupId, group.Key.GroupName, group.Key.MinimumSelection,
                group.Key.MaximumSelection, strategy, group.Key.RequiresCompleteComposition, resolved,
                rules.Where(x => x.SourceGroupId == group.Key.GroupId).ToArray()));
        }
        if (resolvedSelections.Any(x => !selections.Any(y => y.OptionId == x.AdditionalId && y.GroupId == x.GroupId))) return null;
        ModifierCompositionPrice composition;
        try { composition = ModifierCompositionCalculator.Calculate(new Money(row.UnitPrice), compositionGroups); }
        catch (Domain.Exceptions.DomainException) { return null; }
        var snapshots = selections.Select(x => new OrderAdditionalSnapshot(x.OptionId, x.Name, x.UnitPrice, x.Quantity)).ToArray();
        return new(row.ProductId, row.VariationId, row.ProductName, row.VariationName, composition.UnitPrice, snapshots, composition);
    }

    private sealed record OfferRow(Guid ProductId, string ProductName, Guid? VariationId, string? VariationName, decimal UnitPrice);
    private sealed record ModifierRow(Guid GroupId, string GroupName, int MinimumSelection, int MaximumSelection, string PricingStrategy,
        bool RequiresCompleteComposition, Guid? AdditionalId, string? OptionName, decimal? UnitPrice, bool IsAvailable);
    private sealed record CompatibilityRow(Guid SourceGroupId, Guid SourceOptionId, Guid TargetGroupId, Guid TargetOptionId, string Kind);
}

public sealed class OrderCustomerResolver(IReadConnectionFactory connectionFactory) : IOrderCustomerResolver
{
    public async Task<OrderCustomerSnapshot?> ResolveAsync(Guid tenantId, Guid establishmentId, Guid customerId, Guid? addressId, CancellationToken cancellationToken)
    {
        const string sql = """
            select c.id as CustomerId, c.name, c.phone, a.id as AddressId, a.street, a.number, a.complement,
                   a.neighborhood, a.city, a.state, a.postal_code as PostalCode
            from customers.customer c
            left join customers.customer_address a on a.tenant_id = c.tenant_id and a.establishment_id = c.establishment_id
                and a.customer_id = c.id and a.id = @AddressId
            where c.tenant_id = @TenantId and c.establishment_id = @EstablishmentId and c.id = @CustomerId
              and (@AddressId is null or a.id is not null);
            """;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<CustomerRow>(new CommandDefinition(sql, new { TenantId = tenantId, EstablishmentId = establishmentId, CustomerId = customerId, AddressId = addressId }, cancellationToken: cancellationToken));
        if (row is null) return null;
        var address = row.AddressId is null ? null : new DeliveryAddressSnapshot(row.Street!, row.Number!, row.Complement, row.Neighborhood!, row.City!, row.State!, row.PostalCode!);
        return new(row.CustomerId, row.Name, row.Phone, row.AddressId, address);
    }

    private sealed record CustomerRow(Guid CustomerId, string Name, string Phone, Guid? AddressId, string? Street, string? Number, string? Complement, string? Neighborhood, string? City, string? State, string? PostalCode);
}

public sealed class OrderTableResolver(IReadConnectionFactory connectionFactory) : IOrderTableResolver
{
    public async Task<OrderTableSnapshot?> ResolveActiveAsync(Guid tenantId, Guid establishmentId, Guid tableId, CancellationToken cancellationToken)
    {
        const string sql = "select id as TableId, code from operations.service_table where tenant_id = @TenantId and establishment_id = @EstablishmentId and id = @TableId and is_active;";
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<OrderTableSnapshot>(new CommandDefinition(sql, new { TenantId = tenantId, EstablishmentId = establishmentId, TableId = tableId }, cancellationToken: cancellationToken));
    }
}
