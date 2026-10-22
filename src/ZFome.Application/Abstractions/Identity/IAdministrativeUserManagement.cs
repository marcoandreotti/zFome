using ZFome.Application.Identity.Management;
using ZFome.Domain.Identity;

namespace ZFome.Application.Abstractions.Identity;

public interface IAdministrativeUserManagementRepository
{
    Task<AdministrativeUser?> GetAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    Task<bool> IsActiveEstablishmentAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken);
    Task<bool> IsEligiblePlatformUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<(int Owners, int Administrators)> CountOtherAdministratorsAsync(Guid tenantId, Guid excludedUserId, CancellationToken cancellationToken);
    Task<int> CountOtherUnitAdministratorsAsync(Guid tenantId, Guid establishmentId, Guid excludedUserId, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

public interface IAdministrativeUserManagementTransaction
{
    Task ExecuteAsync(Guid tenantId, Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
}

public interface IAdministrativeUserReadGateway
{
    Task<bool> CanManageAsync(Guid tenantId, Guid actorId, bool platformUser, CancellationToken cancellationToken);
    Task<AdministrativeUserSearchResult> SearchAsync(Guid tenantId, SearchAdministrativeUsersQuery query, CancellationToken cancellationToken);
}
