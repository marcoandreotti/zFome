using ZFome.Domain.Catalog;
using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Tests.Catalog;

public sealed class ProductTests
{
    [Fact]
    public void Requires_category_from_same_unit_and_non_negative_price()
    {
        var tenant = Guid.NewGuid(); var establishment = Guid.NewGuid(); var category = Category.Create(tenant, establishment, "Pizzas");
        Assert.Throws<DomainException>(() => Product.Create(tenant, Guid.NewGuid(), category, "P1", "Pizza", Money.Zero));
        Assert.Throws<DomainException>(() => new Money(-1));
    }

    [Fact]
    public void Maintains_one_principal_image_and_variation_price()
    {
        var tenant = Guid.NewGuid(); var establishment = Guid.NewGuid(); var category = Category.Create(tenant, establishment, "Pizzas");
        var product = Product.Create(tenant, establishment, category, "P1", "Pizza", new Money(30));
        product.AddImage("https://example.com/1.jpg", 0, true); var second = product.AddImage("https://example.com/2.jpg", 1, true);
        Assert.Equal(second.Id, Assert.Single(product.Images, image => image.IsPrincipal).Id);
        Assert.Equal(new Money(45), product.AddVariation("Grande", new Money(45), 0).Price);
    }

    [Fact]
    public void Updates_product_collections_and_activation_state()
    {
        var tenant = Guid.NewGuid(); var establishment = Guid.NewGuid();
        var category = Category.Create(tenant, establishment, "Pizzas");
        var product = Product.Create(tenant, establishment, category, "p1", "Pizza", new Money(30));
        product.Update(category, "p2", "Pizza especial", "Descrição", new Money(35), true, false);
        var image = product.AddImage("https://example.com/1.jpg", 0, true);
        var variation = product.AddVariation("Grande", new Money(45), 0);
        variation.Update("Família", new Money(50), 1); variation.Deactivate(); product.Deactivate();
        Assert.Equal("P2", product.Code); Assert.Equal(new Money(35), product.BasePrice); Assert.True(product.IsFeatured); Assert.False(product.IsActive);
        Assert.False(variation.IsActive); Assert.Equal(1, variation.Order);
        product.RemoveImage(image.Id); product.RemoveVariation(variation.Id); product.Activate();
        Assert.Empty(product.Images); Assert.Empty(product.Variations); Assert.True(product.IsActive);
        Assert.Throws<DomainException>(() => product.Update(Category.Create(tenant, Guid.NewGuid(), "Other"), "x", "x", null, Money.Zero, false, true));
    }

    [Fact]
    public void Temporary_unavailability_is_separate_from_administrative_activation()
    {
        var now = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        var value = OfferUnavailability.Create(Guid.NewGuid(), Guid.NewGuid(), OfferKind.Product, Guid.NewGuid(), now, now.AddHours(2), "Sold out");
        Assert.True(value.IsActiveAt(now.AddMinutes(1)));
        value.Reactivate(now.AddMinutes(30));
        Assert.False(value.IsActiveAt(now.AddMinutes(31)));
    }
}
