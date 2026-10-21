namespace ZFome.Application.Abstractions.Tenancy;

public interface IEstablishmentAccessGateway
{
    Task<bool> HasActiveAccessAsync(
        Guid tenantId,
        Guid userId,
        Guid establishmentId,
        CancellationToken cancellationToken);
}
