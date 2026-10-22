using Dapper;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Abstractions.Tenancy;

namespace ZFome.Infrastructure.Persistence.Read;

/// <summary>
/// Representa um gateway de leitura para estabelecimentos, fornecendo métodos para consultar informações de estabelecimentos no banco de dados.
/// </summary>
public sealed class EstablishmentReadGateway(IReadConnectionFactory connectionFactory) : IEstablishmentReadGateway
{
    public async Task<EstablishmentReadModel?> FindAsync(
        Guid tenantId,
        Guid establishmentId,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<EstablishmentReadModel>(new CommandDefinition(
            SelectSql + " and e.tenant_id = @TenantId and e.id = @EstablishmentId",
            new { TenantId = tenantId, EstablishmentId = establishmentId },
            cancellationToken: cancellationToken));
    }

    public async Task<EstablishmentReadModel?> ResolvePublicSlugAsync(
        string normalizedSlug,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<EstablishmentReadModel>(new CommandDefinition(
            SelectSql + " and e.slug = @Slug",
            new { Slug = normalizedSlug },
            cancellationToken: cancellationToken));
    }

    private const string SelectSql =
        """
        select
            e.id as Id,
            e.tenant_id as TenantId,
            e.trade_name as TradeName,
            e.slug as Slug,
            theme.primary_color as PrimaryColor,
            theme.secondary_color as SecondaryColor,
            theme.background_color as BackgroundColor,
            theme.text_color as TextColor,
            theme.font_family as FontFamily,
            theme.logo_url as LogoUrl,
            theme.favicon_url as FaviconUrl
        from tenancy.establishment e
        join tenancy.tenant t on t.id = e.tenant_id
        join tenancy.establishment_theme theme on theme.establishment_id = e.id
        where e.is_active = true and t.is_active = true
        """;
}