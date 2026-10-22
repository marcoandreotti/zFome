using ZFome.Domain.Exceptions;
using ZFome.Domain.Tenancy;

namespace ZFome.Domain.Tests.Tenancy;

public sealed class TenantTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Tenant_is_created_active_and_can_be_deactivated()
    {
        var tenant = Tenant.Create("Grupo Andreotti", Now);

        Assert.True(tenant.IsActive);
        tenant.Deactivate(Now.AddMinutes(1));
        Assert.False(tenant.IsActive);
        Assert.Equal(Now.AddMinutes(1), tenant.UpdatedAt);
    }

    [Fact]
    public void Establishment_requires_a_tenant()
    {
        Assert.Throws<DomainException>(() =>
            Establishment.Create(Guid.Empty, "Pizzaria Centro", new Slug("pizzaria-centro"), Now));
    }

    [Theory]
    [InlineData("Pizzaria Centro", "pizzaria-centro")]
    [InlineData("  LOJA-1  ", "loja-1")]
    public void Slug_is_normalized(string input, string expected)
    {
        Assert.Equal(expected, new Slug(input).Value);
    }

    [Theory]
    [InlineData("invalid_slug")]
    [InlineData("-invalid")]
    [InlineData("ab")]
    public void Slug_rejects_invalid_values(string input)
    {
        Assert.Throws<DomainException>(() => new Slug(input));
    }

    [Fact]
    public void Partial_theme_uses_defaults_and_is_not_public_when_establishment_is_inactive()
    {
        var establishment = Establishment.Create(Guid.NewGuid(), "Centro", new Slug("centro"), Now);
        establishment.ChangeTheme(new EstablishmentTheme(primaryColor: "#ff0000"), Now);

        Assert.Equal("#FF0000", establishment.GetPublicTheme().PrimaryColor);
        Assert.Equal(EstablishmentTheme.DefaultSecondaryColor, establishment.Theme.SecondaryColor);

        establishment.Deactivate(Now);
        Assert.Throws<DomainException>(() => establishment.GetPublicTheme());
    }
}
