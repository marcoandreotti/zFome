using Dapper;
using OrderHub.Application.Abstractions.Catalog;
using OrderHub.Application.Abstractions.Persistence;

namespace OrderHub.Infrastructure.Persistence.Read;

/// <summary>
/// Representa um gateway de leitura para o catálogo, fornecendo métodos para recuperar informações do catálogo, como estabelecimentos, categorias, produtos e grupos adicionais, a partir do banco de dados.
/// </summary>
public sealed class CatalogReadGateway(IReadConnectionFactory connectionFactory) : ICatalogReadGateway
{
    public Task<CatalogReadModel?> GetAdministrativeAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken) => LoadAsync("e.tenant_id = @TenantId and e.id = @EstablishmentId", new { TenantId = tenantId, EstablishmentId = establishmentId }, false, cancellationToken);

    public Task<CatalogReadModel?> GetPublicAsync(string normalizedSlug, CancellationToken cancellationToken) => LoadAsync("e.slug = @Slug and e.is_active = true and t.is_active = true", new { Slug = normalizedSlug }, true, cancellationToken);

    private async Task<CatalogReadModel?> LoadAsync(string establishmentFilter, object parameters, bool publicOnly, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var establishment = await connection.QuerySingleOrDefaultAsync<EstablishmentRow>(new CommandDefinition($"select e.id Id, e.tenant_id TenantId, e.trade_name Name, e.slug Slug from tenancy.establishment e join tenancy.tenant t on t.id=e.tenant_id where {establishmentFilter}", parameters, cancellationToken: cancellationToken));
        if (establishment is null) return null;
        var scope = new { establishment.TenantId, EstablishmentId = establishment.Id };
        var active = publicOnly ? " and is_active = true" : string.Empty;
        var categories = (await connection.QueryAsync<CategoryRow>(new CommandDefinition($"select id Id, parent_category_id ParentId, name Name, description Description, \"order\" \"Order\", image_url ImageUrl, is_active IsActive from catalog.category where tenant_id=@TenantId and establishment_id=@EstablishmentId{active} order by \"order\", name", scope, cancellationToken: cancellationToken))).ToList();
        var products = (await connection.QueryAsync<ProductRow>(new CommandDefinition($"""
            select p.id Id, p.category_id CategoryId, p.code Code, p.name Name, p.description Description,
                p.base_price BasePrice, p.is_featured IsFeatured, p.is_active IsActive, p.allows_notes AllowsNotes,
                case when rs.count > 0 then (rs.rating_sum::numeric / rs.count)::numeric(4,2) end RatingAverage,
                coalesce(rs.count,0) RatingCount
            from catalog.product p left join reviews.rating_summary rs on rs.tenant_id=p.tenant_id
                and rs.establishment_id=p.establishment_id and rs.target_kind='Product' and rs.target_id=p.id
            where p.tenant_id=@TenantId and p.establishment_id=@EstablishmentId{active.Replace("is_active", "p.is_active")} order by p.name
            """, scope, cancellationToken: cancellationToken))).ToList();
        var productIds = products.Select(x => x.Id).ToArray();
        var images = productIds.Length == 0 ? [] : (await connection.QueryAsync<ImageRow>(new CommandDefinition("select id Id, product_id ProductId, url Url, \"order\" \"Order\", is_principal IsPrincipal from catalog.product_image where product_id=any(@Ids) order by \"order\"", new { Ids = productIds }, cancellationToken: cancellationToken))).ToList();
        var variations = productIds.Length == 0 ? [] : (await connection.QueryAsync<VariationRow>(new CommandDefinition($"select id Id, product_id ProductId, name Name, price Price, \"order\" \"Order\", is_active IsActive from catalog.product_variation where product_id=any(@Ids){active} order by \"order\"", new { Ids = productIds }, cancellationToken: cancellationToken))).ToList();
        var groups = (await connection.QueryAsync<GroupRow>(new CommandDefinition($"select g.id Id, g.name Name, g.minimum_selection MinimumSelection, g.maximum_selection MaximumSelection, g.is_active IsActive, g.pricing_strategy PricingStrategy, g.type Type, g.requires_complete_composition RequiresCompleteComposition from catalog.additional_group g where g.tenant_id=@TenantId and g.establishment_id=@EstablishmentId{(publicOnly ? " and g.is_active = true" : string.Empty)}", scope, cancellationToken: cancellationToken))).ToList();
        var groupItems = (await connection.QueryAsync<GroupItemRow>(new CommandDefinition($"select i.group_id GroupId, i.additional_id Id, a.name Name, a.price Price, a.is_active IsActive, i.\"order\" \"Order\" from catalog.additional_group_item i join catalog.additional a on a.tenant_id=i.tenant_id and a.establishment_id=i.establishment_id and a.id=i.additional_id where i.tenant_id=@TenantId and i.establishment_id=@EstablishmentId{(publicOnly ? " and a.is_active = true" : string.Empty)} order by i.\"order\"", scope, cancellationToken: cancellationToken))).ToList();
        var groupIds = groups.Select(x => x.Id).ToArray();
        var compatibilityRules = groupIds.Length == 0 ? [] : (await connection.QueryAsync<CompatibilityRuleRow>(new CommandDefinition("select source_group_id GroupId, source_additional_id AdditionalId, target_group_id TargetGroupId, target_additional_id TargetAdditionalId, kind Kind from catalog.additional_group_compatibility_rule where tenant_id=@TenantId and establishment_id=@EstablishmentId and source_group_id=any(@GroupIds)", new { establishment.TenantId, EstablishmentId = establishment.Id, GroupIds = groupIds }, cancellationToken: cancellationToken))).ToList();
        var links = productIds.Length == 0 ? [] : (await connection.QueryAsync<ProductGroupRow>(new CommandDefinition("select product_id ProductId, group_id GroupId, \"order\" \"Order\" from catalog.product_additional_group where tenant_id=@TenantId and establishment_id=@EstablishmentId and product_id=any(@Ids) order by \"order\"", new { establishment.TenantId, EstablishmentId = establishment.Id, Ids = productIds }, cancellationToken: cancellationToken))).ToList();
        var unavailability = (await connection.QueryAsync<UnavailabilityRow>(new CommandDefinition("""
            select distinct on (kind,offer_id) kind,offer_id as OfferId,reason,ends_at as EndsAt
            from catalog.offer_unavailability
            where tenant_id=@TenantId and establishment_id=@EstablishmentId and reactivated_at is null
              and starts_at<=now() and (ends_at is null or ends_at>now())
            order by kind,offer_id,starts_at desc
            """, scope, cancellationToken: cancellationToken))).ToDictionary(x => (x.Kind, x.OfferId));
        UnavailabilityRow? Unavailable(short kind, Guid id) => unavailability.GetValueOrDefault((kind, id));
        DateTimeOffset? AvailableAgain(UnavailabilityRow? row) => row?.EndsAt is null
            ? null
            : new DateTimeOffset(DateTime.SpecifyKind(row.EndsAt.Value, DateTimeKind.Utc));
        var groupById = groups.ToDictionary(x => x.Id, x => new AdditionalGroupReadModel(x.Id, x.Name, x.MinimumSelection, x.MaximumSelection, x.IsActive, 0, groupItems.Where(i => i.GroupId == x.Id).Select(i => { var u=Unavailable(2,i.Id); var rules = compatibilityRules.Where(rule => rule.GroupId == i.GroupId && rule.AdditionalId == i.Id).Select(rule => new ModifierCompatibilityReadModel(rule.TargetGroupId, rule.TargetAdditionalId, rule.Kind)).ToArray(); return new AdditionalReadModel(i.Id, i.Name, i.Price, i.IsActive, i.Order, u is null, u?.Reason, AvailableAgain(u), rules); }).ToList(), x.PricingStrategy, x.Type, x.RequiresCompleteComposition));
        var productModels = products.Select(p => { var u=Unavailable(0,p.Id); return new ProductReadModel(p.Id, p.Code, p.Name, p.Description, p.BasePrice, p.IsFeatured, p.IsActive, p.AllowsNotes, images.Where(x => x.ProductId == p.Id).Select(x => new ProductImageReadModel(x.Id, x.Url, x.Order, x.IsPrincipal)).ToList(), variations.Where(x => x.ProductId == p.Id).Select(x => { var vu=Unavailable(1,x.Id); return new ProductVariationReadModel(x.Id, x.Name, x.Price, x.Order, x.IsActive, vu is null, vu?.Reason, AvailableAgain(vu)); }).ToList(), links.Where(x => x.ProductId == p.Id && groupById.ContainsKey(x.GroupId)).Select(x => groupById[x.GroupId] with { Order = x.Order }).ToList(), u is null, u?.Reason, AvailableAgain(u), p.RatingAverage, p.RatingCount); }).ToList();
        var categoryModels = categories.Select(c => new CategoryReadModel(c.Id, c.ParentId, c.Name, c.Description, c.Order, c.ImageUrl, c.IsActive, productModels.Where(p => products.Single(x => x.Id == p.Id).CategoryId == c.Id).ToList())).Where(c => !publicOnly || c.Products.Count > 0).ToList();
        return new CatalogReadModel(establishment.Id, establishment.Name, establishment.Slug, categoryModels);
    }

    private sealed record EstablishmentRow(Guid Id, Guid TenantId, string Name, string Slug);
    private sealed record CategoryRow(Guid Id, Guid? ParentId, string Name, string? Description, int Order, string? ImageUrl, bool IsActive);
    private sealed record ProductRow(Guid Id, Guid CategoryId, string Code, string Name, string? Description, decimal BasePrice, bool IsFeatured, bool IsActive, bool AllowsNotes, decimal? RatingAverage, long RatingCount);
    private sealed record ImageRow(Guid Id, Guid ProductId, string Url, int Order, bool IsPrincipal);
    private sealed record VariationRow(Guid Id, Guid ProductId, string Name, decimal Price, int Order, bool IsActive);
    private sealed record GroupRow(Guid Id, string Name, int MinimumSelection, int MaximumSelection, bool IsActive, string PricingStrategy, string Type, bool RequiresCompleteComposition);
    private sealed record GroupItemRow(Guid GroupId, Guid Id, string Name, decimal Price, bool IsActive, int Order);
    private sealed record CompatibilityRuleRow(Guid GroupId, Guid AdditionalId, Guid TargetGroupId, Guid TargetAdditionalId, string Kind);
    private sealed record ProductGroupRow(Guid ProductId, Guid GroupId, int Order);
    private sealed record UnavailabilityRow
    {
        public short Kind { get; init; }
        public Guid OfferId { get; init; }
        public string? Reason { get; init; }
        public DateTime? EndsAt { get; init; }
    }
}
