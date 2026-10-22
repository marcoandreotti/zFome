using System.Collections.Concurrent;

namespace ZFome.Api.Realtime;

public sealed record OrderUpdateSubscription(
    string ConnectionId,
    Guid TenantId,
    Guid EstablishmentId,
    Guid UserId,
    bool IsPlatformUser);

public sealed class OrderUpdateSubscriptions
{
    private readonly ConcurrentDictionary<string, OrderUpdateSubscription> subscriptions = new();

    public void Set(OrderUpdateSubscription subscription) =>
        subscriptions[subscription.ConnectionId] = subscription;

    public void Remove(string connectionId) =>
        subscriptions.TryRemove(connectionId, out _);

    public IReadOnlyList<OrderUpdateSubscription> Get(Guid tenantId, Guid establishmentId) =>
        subscriptions.Values
            .Where(value => value.TenantId == tenantId && value.EstablishmentId == establishmentId)
            .ToArray();
}

public static class OrderUpdateGroups
{
    public static string For(Guid tenantId, Guid establishmentId) =>
        $"orders:{tenantId:N}:{establishmentId:N}";
}
