namespace ZFome.Application.Abstractions.Tenancy;

public interface IEstablishmentReadGateway
{
    Task<EstablishmentReadModel?> FindAsync(
        Guid tenantId,
        Guid establishmentId,
        CancellationToken cancellationToken);

    Task<EstablishmentReadModel?> ResolvePublicSlugAsync(
        string normalizedSlug,
        CancellationToken cancellationToken);
}

public sealed record EstablishmentReadModel(
    Guid Id,
    Guid TenantId,
    string TradeName,
    string Slug,
    string PrimaryColor,
    string SecondaryColor,
    string BackgroundColor,
    string TextColor,
    string FontFamily,
    string? LogoUrl,
    string? FaviconUrl);
