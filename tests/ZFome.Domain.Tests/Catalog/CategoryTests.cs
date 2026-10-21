using ZFome.Domain.Catalog;
using ZFome.Domain.Exceptions;

namespace ZFome.Domain.Tests.Catalog;

public sealed class CategoryTests
{
    [Fact]
    public void Rejects_self_and_indirect_cycles()
    {
        var tenant = Guid.NewGuid(); var establishment = Guid.NewGuid();
        var category = Category.Create(tenant, establishment, "Pizzas");
        Assert.Throws<DomainException>(() => category.ChangeParent(category.Id, tenant, establishment, new HashSet<Guid>()));
        Assert.Throws<DomainException>(() => category.ChangeParent(Guid.NewGuid(), tenant, establishment, new HashSet<Guid> { category.Id }));
    }

    [Fact]
    public void Rejects_parent_from_another_establishment()
    {
        var tenant = Guid.NewGuid(); var category = Category.Create(tenant, Guid.NewGuid(), "Pizzas");
        Assert.Throws<DomainException>(() => category.ChangeParent(Guid.NewGuid(), tenant, Guid.NewGuid(), new HashSet<Guid>()));
    }

    [Fact]
    public void Accepts_same_scope_parent_and_can_move_to_root()
    {
        var tenant = Guid.NewGuid(); var establishment = Guid.NewGuid(); var category = Category.Create(tenant, establishment, "Pizzas"); var parent = Guid.NewGuid();
        category.ChangeParent(parent, tenant, establishment, new HashSet<Guid>()); Assert.Equal(parent, category.ParentCategoryId);
        category.ChangeParent(null, Guid.Empty, Guid.Empty, new HashSet<Guid>()); Assert.Null(category.ParentCategoryId);
    }

    [Fact]
    public void Updates_presentation_order_and_activation_state()
    {
        var category = Category.Create(Guid.NewGuid(), Guid.NewGuid(), "Pizzas");
        category.Update(" Pizzas especiais ", " Artesanais ", 2, "https://example.com/pizzas.jpg");
        category.Deactivate();
        Assert.Equal("Pizzas especiais", category.Name);
        Assert.Equal("Artesanais", category.Description);
        Assert.Equal(2, category.Order);
        Assert.False(category.IsActive);
        category.Activate();
        Assert.True(category.IsActive);
        Assert.Throws<DomainException>(() => category.Update("Pizzas", null, -1, null));
    }
}
