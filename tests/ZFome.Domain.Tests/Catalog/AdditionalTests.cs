using ZFome.Domain.Catalog;
using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Tests.Catalog;

public sealed class AdditionalTests
{
    [Fact]
    public void Group_enforces_range_and_same_unit_items()
    {
        var tenant = Guid.NewGuid(); var establishment = Guid.NewGuid();
        var group = AdditionalGroup.Create(tenant, establishment, "Escolha a borda", 1, 2);
        group.AddItem(Additional.Create(tenant, establishment, "Catupiry", new Money(5)), 0);
        Assert.True(group.IsRequired); group.ValidateSelection(1);
        Assert.Throws<DomainException>(() => group.ValidateSelection(0));
        Assert.Throws<DomainException>(() => group.AddItem(Additional.Create(tenant, Guid.NewGuid(), "Bacon", new Money(4)), 1));
        var category = Category.Create(tenant, establishment, "Pizzas");
        var product = Product.Create(tenant, establishment, category, "P1", "Pizza", new Money(30));
        product.LinkAdditionalGroup(group, 2);
        Assert.Equal(2, Assert.Single(product.AdditionalGroups).Order);
    }

    [Fact]
    public void Group_rejects_incoherent_range()
    {
        Assert.Throws<DomainException>(() => AdditionalGroup.Create(Guid.NewGuid(), Guid.NewGuid(), "Extras", 3, 2));
    }

    [Fact]
    public void Additional_and_group_can_be_maintained_without_losing_invariants()
    {
        var tenant = Guid.NewGuid(); var establishment = Guid.NewGuid();
        var additional = Additional.Create(tenant, establishment, "Bacon", new Money(4));
        additional.Update("Bacon extra", new Money(5)); additional.Deactivate();
        var group = AdditionalGroup.Create(tenant, establishment, "Extras", 0, 2);
        group.AddItem(additional, 1); group.AddItem(additional, 2);
        Assert.Single(group.Items);
        group.Update("Escolha extras", 1, 3); group.Deactivate();
        Assert.True(group.IsRequired); Assert.False(group.IsActive); Assert.False(additional.IsActive);
        group.RemoveItem(additional.Id); group.Activate(); additional.Activate();
        Assert.Empty(group.Items); Assert.True(group.IsActive); Assert.True(additional.IsActive);
        Assert.Throws<DomainException>(() => group.Update("Extras", 4, 3));
    }
}
