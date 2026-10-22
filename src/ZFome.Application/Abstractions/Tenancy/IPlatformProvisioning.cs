using ZFome.Application.Platform;
using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;

namespace ZFome.Application.Abstractions.Tenancy;

public interface IPlatformProvisioningRepository
{
    Task<PlatformProvisioningIntent?> GetIntentAsync(Guid actorId, Guid key, CancellationToken cancellationToken);
    Task<bool> TenantPublicCodeExistsAsync(string publicCode, CancellationToken cancellationToken);
    Task<bool> EstablishmentSlugExistsAsync(string slug, CancellationToken cancellationToken);
    Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<AdministrativeUser?> GetOwnerAsync(Guid tenantId, Guid ownerId, CancellationToken cancellationToken);
    Task AddTenantProvisioningAsync(Tenant tenant, Establishment establishment, AdministrativeUser owner,
        PlatformProvisioningIntent intent, CancellationToken cancellationToken);
    Task AddEstablishmentProvisioningAsync(Establishment establishment, PlatformProvisioningIntent intent,
        CancellationToken cancellationToken);
}

public interface IPlatformProvisioningTransaction
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}

public interface IPlatformProvisioningReadGateway
{
    Task<PlatformTenantSearchResult> SearchTenantsAsync(string? search, bool? isActive, int page, int pageSize,
        CancellationToken cancellationToken);
    Task<PlatformTenantReadModel?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}
