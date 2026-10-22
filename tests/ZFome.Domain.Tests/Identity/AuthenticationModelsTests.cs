using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;

namespace ZFome.Domain.Tests.Identity;

public sealed class AuthenticationModelsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Resend_is_allowed_only_at_or_after_the_interval_boundary()
    {
        var challenge = AuthenticationChallenge.Create(AuthenticationIdentityType.PlatformUser, Guid.NewGuid(), null, "hash", "origin", Now, TimeSpan.FromMinutes(5));
        Assert.False(challenge.CanBeReplacedAt(Now.AddSeconds(59), TimeSpan.FromMinutes(1)));
        Assert.True(challenge.CanBeReplacedAt(Now.AddMinutes(1), TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Tenant_public_code_is_normalized_and_validated()
    { var tenant = Tenant.Create("Grupo", " grupo-01 ", Now); Assert.Equal("GRUPO-01", tenant.PublicCode); Assert.ThrowsAny<Exception>(() => Tenant.Create("Grupo", "x", Now)); }

    [Fact]
    public void Bootstrap_platform_user_requires_password_change()
    { var user = PlatformUser.Bootstrap(new Email("root@example.com"), "hash", Now); Assert.True(user.PasswordChangeRequired); user.ChangePassword("new-hash", Now.AddMinutes(1)); Assert.False(user.PasswordChangeRequired); Assert.Equal("new-hash", user.PasswordHash); }

    [Fact]
    public void Challenge_is_single_use_and_limited()
    { var challenge = AuthenticationChallenge.Create(AuthenticationIdentityType.PlatformUser, Guid.NewGuid(), null, "hash", "origin", Now, TimeSpan.FromMinutes(5)); Assert.True(challenge.CanAttempt(Now, 2)); challenge.Reject(Now, 2); Assert.True(challenge.CanAttempt(Now, 2)); challenge.Consume(Now); Assert.False(challenge.CanAttempt(Now, 2)); }

    [Fact]
    public void Revoked_session_rejects_access_and_refresh()
    { var session = AdministrativeSession.Create(Guid.NewGuid(), AuthenticationIdentityType.PlatformUser, Guid.NewGuid(), null, "a", "r", "c", false, Now, TimeSpan.FromMinutes(5), TimeSpan.FromDays(1)); Assert.True(session.IsAccessValid(Now)); session.Revoke(Now); Assert.False(session.IsAccessValid(Now)); Assert.False(session.IsRefreshValid(Now)); }
}
