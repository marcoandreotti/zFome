using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Exceptions;
using ZFome.Application.Identity;
using ZFome.Application.Identity.CreateAdministrativeUser;
using ZFome.Application.Identity.Management;
using ZFome.Application.Tenancy;
using ZFome.Domain.Identity;

namespace ZFome.Application.Tests.Identity;

public sealed class CreateAdministrativeUserTests
{
    [Fact]
    public async Task Hashes_password_and_scopes_email_uniqueness_to_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeRepository();
        var hasher = new FakeHasher();
        var handler = new CreateAdministrativeUserCommandHandler(
            ManagementFor(tenantId), repository, hasher, TimeProvider.System);

        await handler.HandleAsync(
            new CreateAdministrativeUserCommand("Marco", "marco@example.com", "a-secure-password", AdministrativeRole.Admin, UnitId),
            CancellationToken.None);

        Assert.Equal(tenantId, repository.TenantChecked);
        Assert.Equal("MARCO@EXAMPLE.COM", repository.EmailChecked);
        Assert.Equal("hashed:a-secure-password", repository.Added!.PasswordHash);
    }

    [Fact]
    public async Task Rejects_duplicate_email_in_same_tenant()
    {
        var repository = new FakeRepository { Exists = true };
        var handler = new CreateAdministrativeUserCommandHandler(
            ManagementFor(Guid.NewGuid()), repository, new FakeHasher(), TimeProvider.System);

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            new CreateAdministrativeUserCommand("Marco", "marco@example.com", "a-secure-password", AdministrativeRole.Admin, UnitId),
            CancellationToken.None));
    }

    [Fact]
    public void Policies_map_operational_capabilities_to_explicit_roles()
    {
        Assert.Equal([AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager], AdministrativePolicies.RoleMap[AdministrativePolicies.PromotionManagement]);
        Assert.Equal([AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager, AdministrativeRole.Attendant], AdministrativePolicies.RoleMap[AdministrativePolicies.CustomerOperations]);
        Assert.Equal([AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager, AdministrativeRole.Kitchen], AdministrativePolicies.RoleMap[AdministrativePolicies.OrderKitchen]);
        Assert.Equal([AdministrativeRole.Owner, AdministrativeRole.Admin, AdministrativeRole.Manager, AdministrativeRole.Delivery], AdministrativePolicies.RoleMap[AdministrativePolicies.OrderDelivery]);
        Assert.Equal(Enum.GetValues<AdministrativeRole>(), AdministrativePolicies.RoleMap[AdministrativePolicies.OrderRead]);
    }

    private static readonly Guid UnitId = Guid.NewGuid();

    private static AdministrativeUserManagement ManagementFor(Guid tenantId)
    {
        var actor = AdministrativeUser.Create(tenantId, "Owner", new Email("owner@example.com"), "hash", AdministrativeRole.Owner, DateTimeOffset.UtcNow);
        actor.GrantEstablishmentAccess(UnitId, tenantId, DateTimeOffset.UtcNow);
        var context = new FakeTenantContext(tenantId) { UserId = actor.Id };
        return new(new EstablishmentScopeResolver(context, new Access()), context, new ManagementRepository(actor), new Transaction());
    }

    private sealed class Access : IEstablishmentAccessGateway
    {
        public Task<bool> HasActiveAccessAsync(Guid tenantId, Guid userId, Guid establishmentId, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class Transaction : IAdministrativeUserManagementTransaction
    {
        public Task ExecuteAsync(Guid tenantId, Func<CancellationToken, Task> operation, CancellationToken ct) => operation(ct);
    }

    private sealed class ManagementRepository(AdministrativeUser actor) : IAdministrativeUserManagementRepository
    {
        public Task<AdministrativeUser?> GetAsync(Guid tenantId, Guid userId, CancellationToken ct) => Task.FromResult<AdministrativeUser?>(actor);
        public Task<bool> IsActiveEstablishmentAsync(Guid tenantId, Guid establishmentId, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> IsEligiblePlatformUserAsync(Guid userId, CancellationToken ct) => Task.FromResult(false);
        public Task<(int Owners, int Administrators)> CountOtherAdministratorsAsync(Guid tenantId, Guid excludedUserId, CancellationToken ct) => Task.FromResult((1, 1));
        public Task<int> CountOtherUnitAdministratorsAsync(Guid tenantId, Guid establishmentId, Guid excludedUserId, CancellationToken cancellationToken) => Task.FromResult(1);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeRepository : IAdministrativeUserRepository
    {
        public bool Exists { get; init; }
        public Guid TenantChecked { get; private set; }
        public string? EmailChecked { get; private set; }
        public AdministrativeUser? Added { get; private set; }

        public Task<bool> EmailExistsAsync(Guid tenantId, string normalizedEmail, CancellationToken cancellationToken)
        {
            TenantChecked = tenantId;
            EmailChecked = normalizedEmail;
            return Task.FromResult(Exists);
        }

        public Task AddAsync(AdministrativeUser user, CancellationToken cancellationToken)
        {
            Added = user;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string passwordHash, string password) => passwordHash == Hash(password);
    }

    private sealed class FakeTenantContext(Guid tenantId) : ITenantContext
    {
        public bool HasTenant => true;
        public Guid TenantId => tenantId;
        public bool HasUser => true;
        public Guid UserId { get; init; } = Guid.NewGuid();
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => UserId;
    }
}
