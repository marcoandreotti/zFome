namespace ZFome.Application.Abstractions.Tenancy;

public interface ITenantContext
{
    bool HasTenant { get; }
    Guid TenantId { get; }
    bool HasUser { get; }
    Guid UserId { get; }
    bool IsPlatformUser => false;
    Guid GetRequiredTenantId();
    Guid GetRequiredUserId();
}

public interface IPlatformScopeGateway
{
    Task<Guid?> FindTenantIdAsync(Guid establishmentId, CancellationToken cancellationToken);
}
