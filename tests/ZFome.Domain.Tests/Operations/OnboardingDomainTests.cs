using ZFome.Domain.Exceptions;
using ZFome.Domain.Operations;
using ZFome.Domain.Tenancy;

namespace ZFome.Domain.Tests.Operations;

public sealed class OnboardingDomainTests
{
    [Fact]
    public void Table_changes_preserve_identity_and_rotation_revokes_previous_token()
    {
        var table = ServiceTable.Create(Guid.NewGuid(), Guid.NewGuid(), " a1 ");
        var id = table.Id; var token = table.QrCodeToken; var intent = Guid.NewGuid();
        table.IdentifyCreation(intent); table.Update(" b2 ", " Patio ", false);
        Assert.Equal(id, table.Id); Assert.Equal("B2", table.Code); Assert.Equal("Patio", table.Description); Assert.False(table.IsActive);
        Assert.Throws<DomainException>(() => table.Update("", "bad", true)); Assert.Equal("B2", table.Code); Assert.False(table.IsActive);
        table.Update("B2", null, true); table.RevokeToken();
        Assert.True(table.IsActive); Assert.NotEqual(token, table.QrCodeToken); Assert.Equal(64, table.QrCodeToken.Length); Assert.Equal(intent, table.CreationIntent);
        Assert.Throws<DomainException>(() => table.IdentifyCreation(Guid.NewGuid()));
    }
    [Fact]
    public void Completion_history_is_stable_when_unit_is_edited()
    {
        var now = DateTimeOffset.UtcNow;
        var unit = Establishment.Create(Guid.NewGuid(), "Before", new Slug("before"), now);
        unit.RecordOnboardingCompletion(now); unit.RecordOnboardingCompletion(now.AddDays(1));
        unit.Rename("After", now.AddDays(2));
        Assert.Equal("After", unit.TradeName); Assert.Equal(now, unit.OnboardingCompletedAt);
    }
}
