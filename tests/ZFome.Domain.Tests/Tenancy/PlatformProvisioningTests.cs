using ZFome.Domain.Exceptions;
using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;

namespace ZFome.Domain.Tests.Tenancy;

public sealed class PlatformProvisioningTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Provisioned_owner_is_restricted_until_password_change()
    {
        var user = AdministrativeUser.CreateProvisioned(Guid.NewGuid(), "Owner",
            new Email("owner@example.test"), "temporary-hash", Now);
        Assert.True(user.PasswordChangeRequired);
        Assert.True(user.HasRole(AdministrativeRole.Owner));
        user.ChangePassword("definitive-hash", Now.AddMinutes(1));
        Assert.False(user.PasswordChangeRequired);
        Assert.Equal("definitive-hash", user.PasswordHash);
    }

    [Fact]
    public void Existing_users_remain_unrestricted()
    {
        var user = AdministrativeUser.Create(Guid.NewGuid(), "Manager", new Email("manager@example.test"),
            "hash", AdministrativeRole.Manager, Now);
        Assert.False(user.PasswordChangeRequired);
    }

    [Fact]
    public void Intent_records_actor_hash_and_single_complete_result()
    {
        var intent = PlatformProvisioningIntent.Create(Guid.NewGuid(), Guid.NewGuid(), "HASH",
            PlatformProvisioningKind.Tenant, Now);
        intent.Complete(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.NotNull(intent.CompletedAt);
        Assert.Throws<DomainException>(() => intent.Complete(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now));
    }
}
