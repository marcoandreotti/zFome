using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Exceptions;
using ZFome.Application.Tenancy;

namespace ZFome.Application.Tests.Tenancy;

public sealed class EstablishmentScopeResolverTests
{
    [Fact]
    public async Task Resolves_only_an_active_explicit_association()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var establishmentId = Guid.NewGuid();
        var gateway = new FakeAccessGateway();
        gateway.Grant(tenantId, userId, establishmentId);
        var resolver = new EstablishmentScopeResolver(new FakeTenantContext(tenantId, userId), gateway);

        var scope = await resolver.ResolveAsync(establishmentId, CancellationToken.None);

        Assert.Equal(new OperationalScope(tenantId, userId, establishmentId), scope);
    }

    [Fact]
    public async Task Denies_an_unassociated_unit_in_the_same_tenant()
    {
        var resolver = new EstablishmentScopeResolver(
            new FakeTenantContext(Guid.NewGuid(), Guid.NewGuid()),
            new FakeAccessGateway());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            resolver.ResolveAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Denies_a_unit_associated_to_another_tenant()
    {
        var userId = Guid.NewGuid();
        var establishmentId = Guid.NewGuid();
        var gateway = new FakeAccessGateway();
        gateway.Grant(Guid.NewGuid(), userId, establishmentId);
        var resolver = new EstablishmentScopeResolver(
            new FakeTenantContext(Guid.NewGuid(), userId),
            gateway);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            resolver.ResolveAsync(establishmentId, CancellationToken.None));
    }

    [Fact]
    public async Task Denies_an_association_revoked_before_scope_resolution()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var establishmentId = Guid.NewGuid();
        var gateway = new FakeAccessGateway();
        gateway.Grant(tenantId, userId, establishmentId);
        gateway.Revoke(tenantId, userId, establishmentId);
        var resolver = new EstablishmentScopeResolver(new FakeTenantContext(tenantId, userId), gateway);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            resolver.ResolveAsync(establishmentId, CancellationToken.None));
    }

    private sealed class FakeTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public bool HasTenant => true;
        public Guid TenantId => tenantId;
        public bool HasUser => true;
        public Guid UserId => userId;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
    }

    private sealed class FakeAccessGateway : IEstablishmentAccessGateway
    {
        private readonly HashSet<(Guid TenantId, Guid UserId, Guid EstablishmentId)> grants = [];

        public void Grant(Guid tenantId, Guid userId, Guid establishmentId) =>
            grants.Add((tenantId, userId, establishmentId));

        public void Revoke(Guid tenantId, Guid userId, Guid establishmentId) =>
            grants.Remove((tenantId, userId, establishmentId));

        public Task<bool> HasActiveAccessAsync(
            Guid tenantId,
            Guid userId,
            Guid establishmentId,
            CancellationToken cancellationToken) =>
            Task.FromResult(grants.Contains((tenantId, userId, establishmentId)));
    }
}
