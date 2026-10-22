using ZFome.Domain.Ordering;

namespace ZFome.Application.Abstractions.Ordering;

public interface IOrderSchedulingRepository
{
    Task<OrderSchedulingPolicy?> GetPolicyAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderSchedulingPolicy>> GetPoliciesAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken);
    Task<string?> GetTimeZoneIdAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken);
    Task<int> CountReservedOrdersAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, DateTimeOffset slotStart, CancellationToken cancellationToken);
    void Add(OrderSchedulingPolicy policy);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IOrderSchedulingReadGateway
{
    Task<OrderSchedulingConfigurationReadModel> GetConfigurationAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken);
    Task<OrderSchedulingSlotsReadModel> GetPublicSlotsAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, DateTimeOffset now, CancellationToken cancellationToken);
}

public sealed record OrderSchedulingPolicyReadModel(OrderServiceType ServiceType, bool IsEnabled, int SlotIntervalMinutes, int MinimumAdvanceMinutes, int HorizonDays, int? MaximumOrdersPerSlot);
public sealed record OrderSchedulingConfigurationReadModel(string TimeZoneId, IReadOnlyList<OrderSchedulingPolicyReadModel> Policies);
public sealed record OrderSchedulingSlotsReadModel(OrderServiceType ServiceType, bool IsEnabled, string TimeZoneId, int SlotIntervalMinutes, int MinimumAdvanceMinutes, int HorizonDays, IReadOnlyList<OrderSchedulingSlot> Slots);
