using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Identity.Authentication;
using ZFome.Domain.Identity;

namespace ZFome.Application.Tests.Identity;

public sealed class ProvisionedOwnerAuthenticationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Provisioned_owner_receives_restricted_session_and_changes_password()
    {
        var tenantId = Guid.NewGuid(); var passwords = new Passwords(); var secrets = new Secrets();
        var owner = AdministrativeUser.CreateProvisioned(tenantId, "Owner", new Email("owner@test.local"),
            passwords.Hash("temporary-password"), Now);
        var repository = new Repository(owner);
        var challenge = AuthenticationChallenge.Create(AuthenticationIdentityType.AdministrativeUser, owner.Id,
            tenantId, secrets.Hash("123456"), secrets.Hash("origin"), Now, TimeSpan.FromMinutes(10));
        repository.Challenges.Add(challenge);
        var complete = new ProvisioningCompleteAuthenticationCommandHandler(repository, secrets,
            new Clock(), new AuthenticationOptions());
        var tokens = await complete.HandleAsync(new(challenge.Id, "123456", "origin"), default);
        Assert.True(tokens.PasswordChangeRequired);
        var session = Assert.Single(repository.Sessions);
        Assert.True(session.PasswordChangeRequired);
        var change = new ProvisioningChangeTemporaryPasswordCommandHandler(repository, secrets, passwords, new Clock());
        await change.HandleAsync(new(tokens.AccessToken, "temporary-password", "definitive-password"), default);
        Assert.False(owner.PasswordChangeRequired);
        Assert.True(passwords.Verify(owner.PasswordHash, "definitive-password"));
        Assert.NotNull(session.RevokedAt);
    }

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Passwords : IPasswordHasher { public string Hash(string password) => $"password:{password}"; public bool Verify(string passwordHash, string password) => passwordHash == Hash(password); }
    private sealed class Secrets : IAuthenticationSecretProtector
    {
        private int number; public string GenerateCode() => "123456"; public string GenerateToken() => $"token-{++number}";
        public string Hash(string value) => $"secret:{value}"; public bool Verify(string hash, string value) => hash == Hash(value);
    }
    private sealed class Repository(AdministrativeUser user) : IAuthenticationRepository
    {
        public List<AuthenticationChallenge> Challenges { get; } = []; public List<AdministrativeSession> Sessions { get; } = [];
        public Task<AdministrativeUser?> GetEligibleAdministrativeUserAsync(Guid tenantId, Guid userId, CancellationToken ct) => Task.FromResult<AdministrativeUser?>(user.TenantId == tenantId && user.Id == userId && user.IsActive ? user : null);
        public Task RevokeIdentitySessionsAsync(AuthenticationIdentityType type, Guid identityId, DateTimeOffset now, CancellationToken ct) { foreach (var item in Sessions.Where(x => x.IdentityType == type && x.IdentityId == identityId)) item.Revoke(now); return Task.CompletedTask; }
        public Task<bool> ReplaceChallengeAsync(AuthenticationChallenge challenge, TimeSpan resendInterval, CancellationToken ct) { Challenges.Add(challenge); return Task.FromResult(true); }
        public Task<(Guid TenantId, AdministrativeUser User)?> FindAdministrativeUserAsync(string tenantCode, string normalizedEmail, CancellationToken ct) => Task.FromResult<(Guid, AdministrativeUser)?>(null);
        public Task<PlatformUser?> FindPlatformUserAsync(string normalizedEmail, CancellationToken ct) => Task.FromResult<PlatformUser?>(null);
        public Task<PlatformUser?> GetPlatformUserAsync(Guid id, CancellationToken ct) => Task.FromResult<PlatformUser?>(null);
        public Task<bool> AnyPlatformUserAsync(CancellationToken ct) => Task.FromResult(false);
        public Task<int> CountActivePlatformUsersAsync(CancellationToken ct) => Task.FromResult(0);
        public Task AddPlatformUserAsync(PlatformUser platformUser, CancellationToken ct) => Task.CompletedTask;
        public Task<AuthenticationChallenge?> GetChallengeAsync(Guid id, CancellationToken ct) => Task.FromResult(Challenges.SingleOrDefault(x => x.Id == id));
        public Task AddChallengeAsync(AuthenticationChallenge challenge, CancellationToken ct) { Challenges.Add(challenge); return Task.CompletedTask; }
        public Task<int> CountRecentChallengesAsync(string originHash, DateTimeOffset since, CancellationToken ct) => Task.FromResult(0);
        public Task<AdministrativeSession?> FindSessionByAccessHashAsync(string hash, CancellationToken ct) => Task.FromResult(Sessions.SingleOrDefault(x => x.AccessTokenHash == hash));
        public Task<AdministrativeSession?> FindSessionByRefreshHashAsync(string hash, CancellationToken ct) => Task.FromResult(Sessions.SingleOrDefault(x => x.RefreshTokenHash == hash));
        public Task AddSessionAsync(AdministrativeSession session, CancellationToken ct) { Sessions.Add(session); return Task.CompletedTask; }
        public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct) { foreach (var item in Sessions.Where(x => x.FamilyId == familyId)) item.Revoke(now); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
