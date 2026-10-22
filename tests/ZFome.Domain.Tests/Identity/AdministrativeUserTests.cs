using ZFome.Domain.Exceptions;
using ZFome.Domain.Identity;

namespace ZFome.Domain.Tests.Identity;

public sealed class AdministrativeUserTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Email_is_normalized_and_roles_require_active_user()
    {
        var user = AdministrativeUser.Create(
            Guid.NewGuid(), "Marco", new Email("marco@example.com"), "hash", AdministrativeRole.Owner, Now);

        Assert.Equal("MARCO@EXAMPLE.COM", user.Email.NormalizedValue);
        Assert.True(user.HasRole(AdministrativeRole.Owner));

        user.Deactivate(Now);
        Assert.False(user.HasRole(AdministrativeRole.Owner));
        Assert.Throws<DomainException>(() => user.RecordSuccessfulAccess(Now));
    }

    [Fact]
    public void Establishment_access_is_explicit_revocable_and_same_tenant_only()
    {
        var tenantId = Guid.NewGuid();
        var establishmentId = Guid.NewGuid();
        var user = AdministrativeUser.Create(
            tenantId, "Marco", new Email("marco@example.com"), "hash", AdministrativeRole.Owner, Now);

        user.GrantEstablishmentAccess(establishmentId, tenantId, Now);
        Assert.True(Assert.Single(user.EstablishmentAccesses).IsActive);
        user.RevokeEstablishmentAccess(establishmentId, Now.AddMinutes(1));
        Assert.False(Assert.Single(user.EstablishmentAccesses).IsActive);
        Assert.Throws<DomainException>(() => user.GrantEstablishmentAccess(Guid.NewGuid(), Guid.NewGuid(), Now));
    }
}
