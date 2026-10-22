using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Exceptions;
using ZFome.Application.Platform;
using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;

namespace ZFome.Application.Tests.Tenancy;

public sealed class PlatformProvisioningApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Provisions_complete_tenant_and_repeats_same_intent()
    {
        var repository = new RepositoryFake();
        var handler = Handler(repository, new ResolverFake(AuthenticationIdentityType.PlatformUser, false));
        var command = Command(Guid.NewGuid());
        var first = await handler.HandleAsync(command, default);
        var repeated = await handler.HandleAsync(command, default);
        Assert.Equal(first.TenantId, repeated.TenantId);
        Assert.Equal(first.EstablishmentId, repeated.EstablishmentId);
        Assert.Equal(first.TenantPublicCode, repeated.TenantPublicCode);
        Assert.Single(repository.Tenants);
        var owner = Assert.Single(repository.Owners);
        Assert.True(owner.PasswordChangeRequired);
        Assert.True(owner.HasRole(AdministrativeRole.Owner));
        Assert.Equal(first.EstablishmentId, Assert.Single(owner.EstablishmentAccesses).EstablishmentId);
    }

    [Fact]
    public async Task Rejects_reused_key_with_different_payload()
    {
        var repository = new RepositoryFake(); var key = Guid.NewGuid();
        var handler = Handler(repository, new ResolverFake(AuthenticationIdentityType.PlatformUser, false));
        await handler.HandleAsync(Command(key), default);
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(Command(key) with { TenantName = "Different" }, default));
        Assert.Single(repository.Tenants);
    }

    [Theory]
    [InlineData(AuthenticationIdentityType.AdministrativeUser, false)]
    [InlineData(AuthenticationIdentityType.PlatformUser, true)]
    public async Task Requires_fully_authenticated_platform_user(AuthenticationIdentityType type, bool restricted)
    {
        var handler = Handler(new RepositoryFake(), new ResolverFake(type, restricted));
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.HandleAsync(Command(Guid.NewGuid()), default));
    }

    [Fact]
    public void Validators_reject_invalid_shape()
    {
        var command = Command(Guid.Empty) with { OwnerEmail = "invalid", TemporaryPassword = "short" };
        Assert.False(new ProvisionTenantCommandValidator().Validate(command).IsValid);
        var query = new SearchPlatformTenantsQuery("", (string?)null, null, 0, 101);
        Assert.False(new SearchPlatformTenantsQueryValidator().Validate(query).IsValid);
    }

    private static ProvisionTenantCommandHandler Handler(RepositoryFake repository, IAuthenticationSessionResolver resolver) =>
        new(resolver, repository, new TransactionFake(), new PasswordsFake(), new Clock());
    private static ProvisionTenantCommand Command(Guid key) => new("token", key, "Group", "GROUP-01",
        "Main", "main-unit", "America/Sao_Paulo", "Owner", "owner@example.test", "temporary-password");
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class PasswordsFake : IPasswordHasher { public string Hash(string password) => $"hash:{password}"; public bool Verify(string passwordHash, string password) => passwordHash == Hash(password); }
    private sealed class ResolverFake(AuthenticationIdentityType type, bool restricted) : IAuthenticationSessionResolver
    { public Task<AuthenticatedIdentity?> ResolveAsync(string accessToken, CancellationToken ct) => Task.FromResult<AuthenticatedIdentity?>(new(Guid.NewGuid(), type, Guid.Parse("11111111-1111-1111-1111-111111111111"), null, [], [], restricted)); }
    private sealed class TransactionFake : IPlatformProvisioningTransaction
    { public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) => operation(ct); }

    private sealed class RepositoryFake : IPlatformProvisioningRepository
    {
        public List<Tenant> Tenants { get; } = []; public List<Establishment> Units { get; } = [];
        public List<AdministrativeUser> Owners { get; } = []; public List<PlatformProvisioningIntent> Intents { get; } = [];
        public Task<PlatformProvisioningIntent?> GetIntentAsync(Guid actorId, Guid key, CancellationToken ct) => Task.FromResult(Intents.SingleOrDefault(x => x.ActorId == actorId && x.Key == key));
        public Task<bool> TenantPublicCodeExistsAsync(string publicCode, CancellationToken ct) => Task.FromResult(Tenants.Any(x => x.PublicCode == publicCode));
        public Task<bool> EstablishmentSlugExistsAsync(string slug, CancellationToken ct) => Task.FromResult(Units.Any(x => x.Slug.Value == slug));
        public Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(Tenants.SingleOrDefault(x => x.Id == tenantId));
        public Task<AdministrativeUser?> GetOwnerAsync(Guid tenantId, Guid ownerId, CancellationToken ct) => Task.FromResult(Owners.SingleOrDefault(x => x.TenantId == tenantId && x.Id == ownerId && x.HasRole(AdministrativeRole.Owner)));
        public Task AddTenantProvisioningAsync(Tenant tenant, Establishment establishment, AdministrativeUser owner, PlatformProvisioningIntent intent, CancellationToken ct)
        { Tenants.Add(tenant); Units.Add(establishment); Owners.Add(owner); Intents.Add(intent); return Task.CompletedTask; }
        public Task AddEstablishmentProvisioningAsync(Establishment establishment, PlatformProvisioningIntent intent, CancellationToken ct)
        { Units.Add(establishment); Intents.Add(intent); return Task.CompletedTask; }
    }
}
