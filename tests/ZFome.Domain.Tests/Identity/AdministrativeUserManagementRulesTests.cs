using ZFome.Domain.Identity;

namespace ZFome.Domain.Tests.Identity;

public sealed class AdministrativeUserManagementRulesTests
{
    [Fact]
    public void Removing_last_role_is_rejected_without_changing_user()
    {
        var user = AdministrativeUser.Create(Guid.NewGuid(), "User", new Email("user@example.test"), "hash", AdministrativeRole.Manager, DateTimeOffset.UtcNow);
        Assert.Throws<ZFome.Domain.Exceptions.DomainException>(() => user.RevokeRole(AdministrativeRole.Manager, DateTimeOffset.UtcNow));
        Assert.True(user.HasRole(AdministrativeRole.Manager));
    }
    [Theory]
    [InlineData(AdministrativeRole.Owner, true)]
    [InlineData(AdministrativeRole.Admin, false)]
    [InlineData(AdministrativeRole.Manager, false)]
    [InlineData(AdministrativeRole.Kitchen, false)]
    public void Only_another_active_owner_can_manage_owner(AdministrativeRole role, bool permitted)
    {
        var actor = AdministrativeUser.Create(Guid.NewGuid(), "Actor", new Email("actor@example.test"), "hash", role, DateTimeOffset.UtcNow);
        Assert.Equal(permitted, AdministrativeUserManagementRules.CanManageOwner(actor, Guid.NewGuid()));
        Assert.False(AdministrativeUserManagementRules.CanManageOwner(actor, actor.Id));
        actor.Deactivate(DateTimeOffset.UtcNow);
        Assert.False(AdministrativeUserManagementRules.CanManageOwner(actor, Guid.NewGuid()));
        Assert.Equal(role == AdministrativeRole.Owner, AdministrativeUserManagementRules.IsOwner(actor));
    }

    [Fact]
    public void Active_admin_does_not_replace_last_owner()
    {
        Assert.False(AdministrativeUserManagementRules.PreservesActiveOwner(0, true, [AdministrativeRole.Admin]));
        Assert.False(AdministrativeUserManagementRules.PreservesActiveOwner(0, false, [AdministrativeRole.Owner]));
        Assert.True(AdministrativeUserManagementRules.PreservesActiveOwner(0, true, [AdministrativeRole.Owner]));
        Assert.True(AdministrativeUserManagementRules.PreservesActiveOwner(1, false, []));
        Assert.False(AdministrativeUserManagementRules.PreservesAdministrator(0, true, [AdministrativeRole.Manager]));
    }
}
