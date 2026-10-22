using ZFome.Domain.Delivery;

namespace ZFome.Application.Abstractions.Delivery;

public interface IDeliveryRegionRepository
{
    Task<DeliveryRegion?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken);
    Task AddAsync(DeliveryRegion region, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IDeliveryReadGateway
{
    Task<IReadOnlyList<DeliveryRegionReadModel>> ListAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken);
    Task<DeliveryQuote?> FindQuoteAsync(Guid tenantId, Guid establishmentId, string normalizedPostalCode, CancellationToken cancellationToken);
}

public sealed record DeliveryRegionReadModel(Guid Id, string Name, string PostalCodeFrom, string PostalCodeTo, decimal Fee, int EstimatedMinutes, bool IsActive);
