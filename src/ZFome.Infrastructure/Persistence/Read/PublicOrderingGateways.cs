using Dapper;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Abstractions.PublicOrdering;

namespace ZFome.Infrastructure.Persistence.Read;

/// <summary>
/// Represents a gateway for resolving public ordering context information based on a normalized slug and an optional table token.
/// </summary>
public sealed class PublicOrderingContextGateway(IReadConnectionFactory connectionFactory) : IPublicOrderingContextGateway
{
    public async Task<PublicOrderingContext?> ResolveAsync(string normalizedSlug, string? tableToken, CancellationToken cancellationToken)
    {
        const string sql = """
            select e.tenant_id as TenantId, e.id as EstablishmentId, e.trade_name as EstablishmentName, e.slug,
                   th.primary_color as PrimaryColor, th.secondary_color as SecondaryColor, th.background_color as BackgroundColor,
                   th.text_color as TextColor, th.font_family as FontFamily, th.logo_url as LogoUrl,
                   st.id as TableId, st.code as TableCode, st.qr_code_token as TableToken
            from tenancy.establishment e
            join tenancy.tenant t on t.id=e.tenant_id and t.is_active
            join tenancy.establishment_theme th on th.establishment_id=e.id
            left join operations.service_table st on st.tenant_id=e.tenant_id and st.establishment_id=e.id
                and st.qr_code_token=@TableToken and st.is_active
            where e.slug=@Slug and e.is_active and (@TableToken is null or st.id is not null);
            select pm.id,pm.code,pm.name,pm.is_online as IsOnline,pm.allows_change as AllowsChange
            from payments.payment_method pm
            join tenancy.establishment e on e.tenant_id=pm.tenant_id and e.id=pm.establishment_id
            join tenancy.tenant t on t.id=e.tenant_id
            where e.slug=@Slug and e.is_active and t.is_active and pm.is_active order by pm.name,pm.id;
            """;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { Slug = normalizedSlug, TableToken = tableToken }, cancellationToken: cancellationToken));
        var row = await grid.ReadSingleOrDefaultAsync<ContextRow>();
        var methods = (await grid.ReadAsync<PublicPaymentMethod>()).ToArray();
        return row is null ? null : new(row.TenantId, row.EstablishmentId, row.EstablishmentName, row.Slug, row.PrimaryColor, row.SecondaryColor, row.BackgroundColor, row.TextColor, row.FontFamily, row.LogoUrl, row.TableId, row.TableCode, row.TableToken, methods);
    }

    private sealed record ContextRow(Guid TenantId, Guid EstablishmentId, string EstablishmentName, string Slug, string PrimaryColor, string SecondaryColor, string BackgroundColor, string TextColor, string FontFamily, string? LogoUrl, Guid? TableId, string? TableCode, string? TableToken);
}

/// <summary>
/// Represents a gateway for locating public order information based on a public reference.
/// </summary>
public sealed class PublicOrderLocator(IReadConnectionFactory connectionFactory) : IPublicOrderLocator
{
    public async Task<PublicOrderLocation?> FindAsync(string reference, CancellationToken cancellationToken)
    { await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken); return await connection.QuerySingleOrDefaultAsync<PublicOrderLocation>(new CommandDefinition("select tenant_id as TenantId,establishment_id as EstablishmentId,id as OrderId from orders.\"order\" where public_reference=@Reference", new { Reference = reference.Trim().ToLowerInvariant() }, cancellationToken: cancellationToken)); }
}