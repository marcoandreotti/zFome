using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Ordering;
using ZFome.Application.Tenancy;

namespace ZFome.Application.Tests.Ordering;

public sealed class KitchenDisplayApplicationTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid EstablishmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task Query_uses_authenticated_tenant_and_authorized_establishment()
    {
        var gateway = new Gateway();
        var handler = new GetKitchenQueueQueryHandler(
            new EstablishmentScopeResolver(new TenantContext(), new AccessGateway()),
            gateway);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            new GetKitchenQueueQuery(EstablishmentId),
            cancellation.Token);

        Assert.Empty(result);
        Assert.Equal(TenantId, gateway.TenantId);
        Assert.Equal(EstablishmentId, gateway.EstablishmentId);
        Assert.Equal(cancellation.Token, gateway.CancellationToken);
    }

    [Fact]
    public async Task Validator_rejects_missing_establishment()
    {
        var result = await new GetKitchenQueueQueryValidator().ValidateAsync(
            new GetKitchenQueueQuery(Guid.Empty));

        Assert.False(result.IsValid);
    }

    private sealed class TenantContext : ITenantContext
    {
        public bool HasTenant => true;
        public Guid TenantId => KitchenDisplayApplicationTests.TenantId;
        public bool HasUser => true;
        public Guid UserId => KitchenDisplayApplicationTests.UserId;
        public Guid GetRequiredTenantId() => TenantId;
        public Guid GetRequiredUserId() => UserId;
    }

    private sealed class AccessGateway : IEstablishmentAccessGateway
    {
        public Task<bool> HasActiveAccessAsync(
            Guid tenantId,
            Guid userId,
            Guid establishmentId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                tenantId == TenantId &&
                userId == UserId &&
                establishmentId == EstablishmentId);
    }

    private sealed class Gateway : IKitchenDisplayReadGateway
    {
        public Guid TenantId { get; private set; }
        public Guid EstablishmentId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task<IReadOnlyList<KitchenTicketReadModel>> GetQueueAsync(
            Guid tenantId,
            Guid establishmentId,
            CancellationToken cancellationToken)
        {
            TenantId = tenantId;
            EstablishmentId = establishmentId;
            CancellationToken = cancellationToken;
            return Task.FromResult<IReadOnlyList<KitchenTicketReadModel>>([]);
        }
    }
}
