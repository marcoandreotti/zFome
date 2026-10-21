using ZFome.Domain.Catalog;
using ZFome.Domain.Exceptions;
using ZFome.Domain.Ordering;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Tests.Ordering;

public sealed class ModifierCompositionTests
{
    [Fact]
    public void Highest_price_uses_the_most_expensive_selected_option_and_replaces_base_price()
    {
        var groupId = Guid.NewGuid();
        var group = Compose(groupId, ModifierPricingStrategy.HighestPrice,
        [
            Selection(groupId, 40m, new ModifierPortion(1, 2)),
            Selection(groupId, 50m, new ModifierPortion(1, 2))
        ]);

        var result = ModifierCompositionCalculator.Calculate(new Money(30m), [group]);

        Assert.Equal(new Money(50m), result.UnitPrice);
        Assert.Equal(new Money(30m), result.BasePrice);
        Assert.Equal(new Money(50m), Assert.Single(result.Groups).Price);
    }

    [Fact]
    public void Proportional_prices_two_and_three_flavors_using_exact_portions()
    {
        var twoFlavorGroupId = Guid.NewGuid();
        var twoFlavors = Compose(twoFlavorGroupId, ModifierPricingStrategy.Proportional,
        [
            Selection(twoFlavorGroupId, 40m, new ModifierPortion(1, 2)),
            Selection(twoFlavorGroupId, 50m, new ModifierPortion(1, 2))
        ]);
        var threeFlavorGroupId = Guid.NewGuid();
        var threeFlavors = Compose(threeFlavorGroupId, ModifierPricingStrategy.Proportional,
        [
            Selection(threeFlavorGroupId, 42m, new ModifierPortion(1, 3)),
            Selection(threeFlavorGroupId, 48m, new ModifierPortion(1, 3)),
            Selection(threeFlavorGroupId, 60m, new ModifierPortion(1, 3))
        ]);

        Assert.Equal(new Money(45m), ModifierCompositionCalculator.Calculate(new Money(0m), [twoFlavors]).UnitPrice);
        Assert.Equal(new Money(50m), ModifierCompositionCalculator.Calculate(new Money(0m), [threeFlavors]).UnitPrice);
    }

    [Fact]
    public void Additive_groups_are_added_and_no_price_change_group_costs_zero()
    {
        var flavorId = Guid.NewGuid();
        var crustId = Guid.NewGuid();
        var extraId = Guid.NewGuid();
        var removalId = Guid.NewGuid();
        var groups = new[]
        {
            Compose(flavorId, ModifierPricingStrategy.HighestPrice, [Selection(flavorId, 50m, new ModifierPortion(1, 1))]),
            Compose(crustId, ModifierPricingStrategy.Additive, [Selection(crustId, 8m, null)]),
            Compose(extraId, ModifierPricingStrategy.Additive, [Selection(extraId, 5m, null)]),
            Compose(removalId, ModifierPricingStrategy.NoPriceChange, [Selection(removalId, 0m, null)])
        };

        var result = ModifierCompositionCalculator.Calculate(new Money(99m), groups);

        Assert.Equal(new Money(63m), result.UnitPrice);
        Assert.Equal(Money.Zero, result.Groups.Single(x => x.GroupId == removalId).Price);
    }

    [Fact]
    public void Rejects_incomplete_portions_and_missing_required_options()
    {
        var groupId = Guid.NewGuid();
        var incomplete = Compose(groupId, ModifierPricingStrategy.Proportional,
            [Selection(groupId, 40m, new ModifierPortion(1, 3))], minimum: 1, maximum: 2);

        Assert.Throws<DomainException>(() => ModifierCompositionCalculator.Calculate(Money.Zero, [incomplete]));

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var option = Selection(first, 1m, null, optionId);
        var rule = new ModifierCompatibilityRuleInput(first, optionId, second, Guid.NewGuid(), ModifierCompatibilityKind.Requires);
        var source = Compose(first, ModifierPricingStrategy.Additive, [option], [rule]);
        var target = Compose(second, ModifierPricingStrategy.Additive, []);

        Assert.Throws<DomainException>(() => ModifierCompositionCalculator.Calculate(Money.Zero, [source, target]));
    }

    [Fact]
    public void Excludes_is_mutual_even_when_only_configured_from_one_option()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var firstOption = Guid.NewGuid();
        var secondOption = Guid.NewGuid();
        var rule = new ModifierCompatibilityRuleInput(first, firstOption, second, secondOption, ModifierCompatibilityKind.Excludes);
        var source = Compose(first, ModifierPricingStrategy.Additive, [Selection(first, 1m, null, firstOption)], [rule]);
        var target = Compose(second, ModifierPricingStrategy.Additive, [Selection(second, 1m, null, secondOption)]);

        Assert.Throws<DomainException>(() => ModifierCompositionCalculator.Calculate(Money.Zero, [source, target]));
    }

    [Fact]
    public void Product_allows_only_one_composition_pricing_group_and_requires_rule_target_groups()
    {
        var tenantId = Guid.NewGuid();
        var establishmentId = Guid.NewGuid();
        var category = Category.Create(tenantId, establishmentId, "Pizzas");
        var product = Product.Create(tenantId, establishmentId, category, "P1", "Pizza", Money.Zero);
        var first = AdditionalGroup.Create(tenantId, establishmentId, "Sabores", 1, 2, ModifierPricingStrategy.HighestPrice, ModifierGroupType.Flavor, true);
        var second = AdditionalGroup.Create(tenantId, establishmentId, "Tamanhos", 1, 2, ModifierPricingStrategy.Proportional, ModifierGroupType.Flavor, true);

        Assert.Throws<DomainException>(() => product.ReplaceAdditionalGroups([(first, 0), (second, 1)]));
    }

    private static ModifierGroupCompositionInput Compose(
        Guid groupId,
        ModifierPricingStrategy strategy,
        IReadOnlyList<ModifierOptionSelectionInput> selections,
        IReadOnlyList<ModifierCompatibilityRuleInput>? rules = null,
        int minimum = 1,
        int maximum = 4) => new(groupId, "Grupo", minimum, maximum, strategy,
            strategy is ModifierPricingStrategy.HighestPrice or ModifierPricingStrategy.Proportional, selections, rules ?? []);

    private static ModifierOptionSelectionInput Selection(Guid groupId, decimal price, ModifierPortion? portion, Guid? optionId = null) =>
        new(groupId, optionId ?? Guid.NewGuid(), "Opção", new Money(price), new Quantity(1m), portion);
}
