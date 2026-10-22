using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Exceptions;
using ZFome.Application.Identity.Authentication;
using ZFome.Domain.Identity;

namespace ZFome.Application.Tests.Identity;

public sealed class AdministrativeAuthenticationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("123456789", false)]
    [InlineData("1234567890", true)]
    public void Password_policy_requires_at_least_ten_characters(string password, bool expected)
    {
        var changeCommand = new ChangeTemporaryPasswordCommand("access", "current-password", password);
        var createPlatformCommand = new CreatePlatformUserCommand("access", "root@example.test", password);
        var change = new ChangeTemporaryPasswordCommandValidator().Validate(changeCommand);
        var createPlatform = new CreatePlatformUserCommandValidator().Validate(createPlatformCommand);

        Assert.Equal(expected, change.IsValid);
        Assert.Equal(expected, createPlatform.IsValid);
    }

    [Fact]
    public async Task Platform_login_creates_restricted_session_until_temporary_password_is_changed()
    {
        var repository = new AuthenticationRepositoryFake();
        var secrets = new AuthenticationSecretProtectorFake();
        var passwords = new PasswordHasherFake();
        var sender = new AuthenticationCodeSenderFake();
        var clock = new FixedTimeProvider(Now);
        var options = new AuthenticationOptions { PlatformCode = "ZFOME" };
        var platformUser = PlatformUser.Bootstrap(new Email("root@zfome.test"), passwords.Hash("temporary-password"), Now);
        repository.PlatformUsers.Add(platformUser);

        var begin = new BeginAuthenticationCommandHandler(repository, passwords, secrets, sender, clock, options);
        var challenge = await begin.HandleAsync(
            new BeginAuthenticationCommand("zfome", "root@zfome.test", "temporary-password", "127.0.0.1"),
            CancellationToken.None);

        Assert.Equal("root@zfome.test", sender.Email);
        Assert.Equal("123456", sender.Code);

        var complete = new CompleteAuthenticationCommandHandler(repository, secrets, clock, options);
        var tokens = await complete.HandleAsync(
            new CompleteAuthenticationCommand(challenge.ChallengeId, sender.Code!, "127.0.0.1"),
            CancellationToken.None);

        Assert.True(tokens.PasswordChangeRequired);
        var session = Assert.Single(repository.Sessions);
        Assert.Equal(AuthenticationIdentityType.PlatformUser, session.IdentityType);
        Assert.Null(session.TenantId);

        var restrictedResolver = new SessionResolverFake(platformUser.Id, passwordChangeRequired: true);
        var createUser = new CreatePlatformUserCommandHandler(restrictedResolver, repository, passwords, clock);
        await Assert.ThrowsAsync<ForbiddenException>(() => createUser.HandleAsync(
            new CreatePlatformUserCommand(tokens.AccessToken, "peer@zfome.test", "another-password"),
            CancellationToken.None));

        var changePassword = new ChangeTemporaryPasswordCommandHandler(repository, secrets, passwords, clock);
        await changePassword.HandleAsync(
            new ChangeTemporaryPasswordCommand(tokens.AccessToken, "temporary-password", "definitive-password"),
            CancellationToken.None);

        Assert.False(platformUser.PasswordChangeRequired);
        Assert.False(session.IsAccessValid(Now));
    }

    [Fact]
    public async Task Fully_authenticated_superuser_can_create_a_peer_but_tenant_user_cannot()
    {
        var repository = new AuthenticationRepositoryFake();
        var passwords = new PasswordHasherFake();
        var clock = new FixedTimeProvider(Now);
        var actorId = Guid.NewGuid();
        var platformResolver = new SessionResolverFake(actorId, passwordChangeRequired: false);
        var handler = new CreatePlatformUserCommandHandler(platformResolver, repository, passwords, clock);

        var createdId = await handler.HandleAsync(
            new CreatePlatformUserCommand("access", "peer@zfome.test", "temporary-password"),
            CancellationToken.None);

        Assert.Equal(createdId, Assert.Single(repository.PlatformUsers).Id);
        var tenantResolver = new SessionResolverFake(actorId, passwordChangeRequired: false, AuthenticationIdentityType.AdministrativeUser);
        var forbiddenHandler = new CreatePlatformUserCommandHandler(tenantResolver, repository, passwords, clock);
        await Assert.ThrowsAsync<ForbiddenException>(() => forbiddenHandler.HandleAsync(
            new CreatePlatformUserCommand("access", "other@zfome.test", "temporary-password"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Same_email_in_two_tenants_is_resolved_only_by_the_matching_public_code()
    {
        var firstTenantId = Guid.NewGuid();
        var secondTenantId = Guid.NewGuid();
        var passwords = new PasswordHasherFake();
        var repository = new AuthenticationRepositoryFake();
        repository.AdministrativeUsers["FIRST"] = AdministrativeUser.Create(firstTenantId, "First", new Email("admin@example.test"), passwords.Hash("first-password"), AdministrativeRole.Owner, Now);
        repository.AdministrativeUsers["SECOND"] = AdministrativeUser.Create(secondTenantId, "Second", new Email("admin@example.test"), passwords.Hash("second-password"), AdministrativeRole.Owner, Now);
        var handler = new BeginAuthenticationCommandHandler(repository, passwords, new AuthenticationSecretProtectorFake(), new AuthenticationCodeSenderFake(), new FixedTimeProvider(Now), new AuthenticationOptions());

        var result = await handler.HandleAsync(
            new BeginAuthenticationCommand(" second ", "admin@example.test", "second-password", "127.0.0.1"),
            CancellationToken.None);

        Assert.Equal(secondTenantId, Assert.Single(repository.Challenges).TenantId);
        await Assert.ThrowsAsync<UnauthorizedException>(() => handler.HandleAsync(
            new BeginAuthenticationCommand("FIRST", "admin@example.test", "second-password", "127.0.0.2"),
            CancellationToken.None));
        Assert.NotEqual(Guid.Empty, result.ChallengeId);
    }

    [Fact]
    public async Task Disabled_tenant_user_cannot_refresh_and_all_families_are_revoked()
    {
        var repository = new AuthenticationRepositoryFake();
        var secrets = new AuthenticationSecretProtectorFake();
        var user = AdministrativeUser.Create(Guid.NewGuid(), "Admin", new Email("admin@example.test"), "hash", AdministrativeRole.Admin, Now);
        repository.AdministrativeUsers["TENANT"] = user;
        user.Deactivate(Now);
        repository.Sessions.Add(AdministrativeSession.Create(Guid.NewGuid(), AuthenticationIdentityType.AdministrativeUser, user.Id, user.TenantId,
            secrets.Hash("access"), secrets.Hash("refresh"), secrets.Hash("csrf"), false, Now, TimeSpan.FromMinutes(5), TimeSpan.FromDays(1)));
        var handler = new RefreshAuthenticationCommandHandler(repository, secrets, new FixedTimeProvider(Now), new AuthenticationOptions());
        await Assert.ThrowsAsync<UnauthorizedException>(() => handler.HandleAsync(new("refresh", "csrf"), CancellationToken.None));
        Assert.Single(repository.Sessions);
        Assert.All(repository.Sessions, session => Assert.NotNull(session.RevokedAt));
    }

    [Fact]
    public async Task Password_change_revokes_other_families_and_pending_challenges_but_not_other_users()
    {
        var repository = new AuthenticationRepositoryFake();
        var secrets = new AuthenticationSecretProtectorFake();
        var passwords = new PasswordHasherFake();
        var user = PlatformUser.Bootstrap(new Email("root@example.test"), passwords.Hash("temporary-password"), Now);
        repository.PlatformUsers.Add(user);
        foreach (var token in new[] { "current", "other" }) repository.Sessions.Add(AdministrativeSession.Create(Guid.NewGuid(), AuthenticationIdentityType.PlatformUser,
            user.Id, null, secrets.Hash(token), secrets.Hash(token + "-refresh"), secrets.Hash("csrf"), true, Now, TimeSpan.FromMinutes(5), TimeSpan.FromDays(1)));
        var unrelated = AdministrativeSession.Create(Guid.NewGuid(), AuthenticationIdentityType.PlatformUser, Guid.NewGuid(), null, "a", "r", "c", true, Now, TimeSpan.FromMinutes(5), TimeSpan.FromDays(1));
        repository.Sessions.Add(unrelated);
        repository.Challenges.Add(AuthenticationChallenge.Create(AuthenticationIdentityType.PlatformUser, user.Id, null, "code", "origin", Now, TimeSpan.FromMinutes(5)));
        await new ChangeTemporaryPasswordCommandHandler(repository, secrets, passwords, new FixedTimeProvider(Now))
            .HandleAsync(new("current", "temporary-password", "definitive-password"), CancellationToken.None);
        Assert.All(repository.Sessions.Where(session => session.IdentityId == user.Id), session => Assert.NotNull(session.RevokedAt));
        Assert.NotNull(Assert.Single(repository.Challenges).ConsumedAt);
        Assert.Null(unrelated.RevokedAt);
    }

    [Fact]
    public async Task Resend_observes_cooldown_and_invalidates_previous_code()
    {
        var repository = new AuthenticationRepositoryFake();
        var passwords = new PasswordHasherFake();
        repository.PlatformUsers.Add(PlatformUser.Bootstrap(new Email("root@example.test"), passwords.Hash("password"), Now));
        var secrets = new AuthenticationSecretProtectorFake();
        var sender = new AuthenticationCodeSenderFake();
        var command = new BeginAuthenticationCommand("PLATFORM", "root@example.test", "password", "origin");
        var handler = new BeginAuthenticationCommandHandler(repository, passwords, secrets, sender, new FixedTimeProvider(Now), new AuthenticationOptions());
        var first = await handler.HandleAsync(command, CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(command, CancellationToken.None));
        var later = new BeginAuthenticationCommandHandler(repository, passwords, secrets, sender, new FixedTimeProvider(Now.AddMinutes(2)), new AuthenticationOptions());
        await later.HandleAsync(command, CancellationToken.None);
        Assert.NotNull(repository.Challenges.Single(item => item.Id == first.ChallengeId).ConsumedAt);
        await Assert.ThrowsAsync<UnauthorizedException>(() => new CompleteAuthenticationCommandHandler(repository, secrets, new FixedTimeProvider(Now.AddMinutes(2)), new AuthenticationOptions())
            .HandleAsync(new(first.ChallengeId, "123456", "origin"), CancellationToken.None));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PasswordHasherFake : IPasswordHasher
    {
        public string Hash(string password) => $"password:{password}";
        public bool Verify(string passwordHash, string password) => passwordHash == Hash(password);
    }

    private sealed class AuthenticationSecretProtectorFake : IAuthenticationSecretProtector
    {
        private int tokenNumber;
        public string GenerateCode() => "123456";
        public string GenerateToken() => $"token-{Interlocked.Increment(ref tokenNumber)}";
        public string Hash(string value) => $"secret:{value}";
        public bool Verify(string hash, string value) => hash == Hash(value);
    }

    private sealed class AuthenticationCodeSenderFake : IAuthenticationCodeSender
    {
        public string? Email { get; private set; }
        public string? Code { get; private set; }
        public Task SendAsync(string email, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken)
        {
            Email = email;
            Code = code;
            return Task.CompletedTask;
        }
    }

    private sealed class SessionResolverFake(Guid identityId, bool passwordChangeRequired, AuthenticationIdentityType type = AuthenticationIdentityType.PlatformUser) : IAuthenticationSessionResolver
    {
        public Task<AuthenticatedIdentity?> ResolveAsync(string accessToken, CancellationToken cancellationToken) =>
            Task.FromResult<AuthenticatedIdentity?>(new(Guid.NewGuid(), type, identityId, null, [], [], passwordChangeRequired));
    }

    private sealed class AuthenticationRepositoryFake : IAuthenticationRepository
    {
        public Task<AdministrativeUser?> GetEligibleAdministrativeUserAsync(Guid tenantId, Guid userId, CancellationToken ct) =>
            Task.FromResult(AdministrativeUsers.Values.SingleOrDefault(user => user.TenantId == tenantId && user.Id == userId && user.IsActive));
        public Task RevokeIdentitySessionsAsync(AuthenticationIdentityType type, Guid identityId, DateTimeOffset now, CancellationToken ct)
        {
            foreach (var session in Sessions.Where(item => item.IdentityType == type && item.IdentityId == identityId)) session.Revoke(now);
            foreach (var challenge in Challenges.Where(item => item.IdentityType == type && item.IdentityId == identityId && item.ConsumedAt == null)) challenge.Consume(now);
            return Task.CompletedTask;
        }
        public Task<bool> ReplaceChallengeAsync(AuthenticationChallenge challenge, TimeSpan resendInterval, CancellationToken ct)
        {
            var previous = Challenges.Where(item => item.IdentityType == challenge.IdentityType && item.IdentityId == challenge.IdentityId).ToArray();
            if (previous.Any(item => item.CreatedAt > challenge.CreatedAt - resendInterval)) return Task.FromResult(false);
            foreach (var item in previous.Where(item => item.ConsumedAt == null)) item.Consume(challenge.CreatedAt);
            Challenges.Add(challenge);
            return Task.FromResult(true);
        }
        public List<PlatformUser> PlatformUsers { get; } = [];
        public Dictionary<string, AdministrativeUser> AdministrativeUsers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<AuthenticationChallenge> Challenges { get; } = [];
        public List<AdministrativeSession> Sessions { get; } = [];

        public Task<(Guid TenantId, AdministrativeUser User)?> FindAdministrativeUserAsync(string tenantCode, string normalizedEmail, CancellationToken cancellationToken)
        {
            var code = tenantCode.Trim();
            var match = AdministrativeUsers.GetValueOrDefault(code);
            return Task.FromResult<(Guid, AdministrativeUser)?>(match is not null && match.NormalizedEmail == normalizedEmail ? (match.TenantId, match) : null);
        }

        public Task<PlatformUser?> FindPlatformUserAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(PlatformUsers.SingleOrDefault(x => x.NormalizedEmail == normalizedEmail));
        public Task<PlatformUser?> GetPlatformUserAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(PlatformUsers.SingleOrDefault(x => x.Id == id));
        public Task<bool> AnyPlatformUserAsync(CancellationToken cancellationToken) => Task.FromResult(PlatformUsers.Count != 0);
        public Task<int> CountActivePlatformUsersAsync(CancellationToken cancellationToken) => Task.FromResult(PlatformUsers.Count(x => x.IsActive));
        public Task AddPlatformUserAsync(PlatformUser user, CancellationToken cancellationToken) { PlatformUsers.Add(user); return Task.CompletedTask; }
        public Task<AuthenticationChallenge?> GetChallengeAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Challenges.SingleOrDefault(x => x.Id == id));
        public Task AddChallengeAsync(AuthenticationChallenge challenge, CancellationToken cancellationToken) { Challenges.Add(challenge); return Task.CompletedTask; }
        public Task<int> CountRecentChallengesAsync(string originHash, DateTimeOffset since, CancellationToken cancellationToken) => Task.FromResult(Challenges.Count(x => x.OriginHash == originHash && x.CreatedAt >= since));
        public Task<AdministrativeSession?> FindSessionByAccessHashAsync(string hash, CancellationToken cancellationToken) => Task.FromResult(Sessions.SingleOrDefault(x => x.AccessTokenHash == hash));
        public Task<AdministrativeSession?> FindSessionByRefreshHashAsync(string hash, CancellationToken cancellationToken) => Task.FromResult(Sessions.SingleOrDefault(x => x.RefreshTokenHash == hash));
        public Task AddSessionAsync(AdministrativeSession session, CancellationToken cancellationToken) { Sessions.Add(session); return Task.CompletedTask; }
        public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) { foreach (var session in Sessions.Where(x => x.FamilyId == familyId)) session.Revoke(now); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
