using System.Data;
using Dapper;
using ZFome.Application.Abstractions.Catalog;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Catalog;

namespace ZFome.Infrastructure.Persistence.Read;

public sealed class CatalogMaintenanceReadGateway(IReadConnectionFactory connections) : ICatalogMaintenanceReadGateway
{
    // Filtros parametrizados: vínculo e atividade de produtos não condicionam a manutenção.
    private const string Filter = """
        where tenant_id = @TenantId and establishment_id = @EstablishmentId
        and (@IsActive is null or is_active = @IsActive)
        and (@Search is null or strpos(lower(name), lower(@Search)) > 0)
        """;

    public async Task<AdditionalSearchResult> SearchAdditionalsAsync(Guid tenantId, SearchAdditionalsQuery query, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var parameters = Parameters(tenantId, query.EstablishmentId, query.Search, query.IsActive, query.Page, query.PageSize);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition("select count(*)::int from catalog.additional " + Filter, parameters, transaction, cancellationToken: ct));
        var rows = await connection.QueryAsync<AdditionalRow>(new CommandDefinition(
            "select id Id, name Name, price Price, is_active IsActive, 0 as \"Order\" from catalog.additional " + Filter + " order by name, id limit @PageSize offset @Offset", parameters, transaction, cancellationToken: ct));
        await transaction.CommitAsync(ct);
        return new(total, rows.Select(x => new AdditionalReadModel(x.Id, x.Name, x.Price, x.IsActive, x.Order)).ToArray());
    }

    public async Task<AdditionalGroupSearchResult> SearchGroupsAsync(Guid tenantId, SearchAdditionalGroupsQuery query, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var parameters = Parameters(tenantId, query.EstablishmentId, query.Search, query.IsActive, query.Page, query.PageSize);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition("select count(*)::int from catalog.additional_group " + Filter, parameters, transaction, cancellationToken: ct));
        var groups = (await connection.QueryAsync<GroupRow>(new CommandDefinition(
            "select id Id, name Name, minimum_selection MinimumSelection, maximum_selection MaximumSelection, is_active IsActive, pricing_strategy PricingStrategy, type Type, requires_complete_composition RequiresCompleteComposition from catalog.additional_group " + Filter + " order by name, id limit @PageSize offset @Offset", parameters, transaction, cancellationToken: ct))).ToArray();
        var ids = groups.Select(group => group.Id).ToArray();
        var items = ids.Length == 0 ? [] : (await connection.QueryAsync<ItemRow>(new CommandDefinition("""
            select i.group_id GroupId, a.id Id, a.name Name, a.price Price, a.is_active IsActive, i."order" "Order"
            from catalog.additional_group_item i
            join catalog.additional a on a.id = i.additional_id and a.tenant_id = i.tenant_id and a.establishment_id = i.establishment_id
            where i.tenant_id = @TenantId and i.establishment_id = @EstablishmentId and i.group_id = any(@Ids)
            order by i."order", a.id
            """, new { TenantId = tenantId, query.EstablishmentId, Ids = ids }, transaction, cancellationToken: ct))).ToArray();
        var compatibilityRules = ids.Length == 0 ? [] : (await connection.QueryAsync<CompatibilityRuleRow>(new CommandDefinition("""
            select source_group_id GroupId, source_additional_id AdditionalId, target_group_id TargetGroupId, target_additional_id TargetAdditionalId, kind Kind
            from catalog.additional_group_compatibility_rule
            where tenant_id=@TenantId and establishment_id=@EstablishmentId and source_group_id=any(@Ids)
            """, new { TenantId = tenantId, query.EstablishmentId, Ids = ids }, transaction, cancellationToken: ct))).ToArray();
        await transaction.CommitAsync(ct);
        return new(total, groups.Select(group => new AdditionalGroupReadModel(group.Id, group.Name, group.MinimumSelection, group.MaximumSelection, group.IsActive, 0,
            items.Where(item => item.GroupId == group.Id).Select(item => new AdditionalReadModel(item.Id, item.Name, item.Price, item.IsActive, item.Order,
                CompatibilityRules: compatibilityRules.Where(rule => rule.GroupId == group.Id && rule.AdditionalId == item.Id)
                    .Select(rule => new ModifierCompatibilityReadModel(rule.TargetGroupId, rule.TargetAdditionalId, rule.Kind)).ToArray())).ToArray(),
            group.PricingStrategy, group.Type, group.RequiresCompleteComposition)).ToArray());
    }

    private static object Parameters(Guid tenantId, Guid establishmentId, string? search, bool? isActive, int page, int pageSize) =>
        new { TenantId = tenantId, EstablishmentId = establishmentId, Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(), IsActive = isActive, PageSize = pageSize, Offset = (page - 1) * pageSize };
    private sealed record AdditionalRow(Guid Id, string Name, decimal Price, bool IsActive, int Order);
    private sealed record GroupRow(Guid Id, string Name, int MinimumSelection, int MaximumSelection, bool IsActive, string PricingStrategy, string Type, bool RequiresCompleteComposition);
    private sealed record ItemRow(Guid GroupId, Guid Id, string Name, decimal Price, bool IsActive, int Order);
    private sealed record CompatibilityRuleRow(Guid GroupId, Guid AdditionalId, Guid TargetGroupId, Guid TargetAdditionalId, string Kind);
}
