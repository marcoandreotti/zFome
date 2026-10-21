using System.Numerics;
using ZFome.Domain.Catalog;
using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Ordering;

public sealed record ModifierCompatibilityRuleInput(
    Guid SourceGroupId,
    Guid SourceOptionId,
    Guid TargetGroupId,
    Guid TargetOptionId,
    ModifierCompatibilityKind Kind);

public sealed record ModifierOptionSelectionInput(
    Guid GroupId,
    Guid OptionId,
    string Name,
    Money UnitPrice,
    Quantity Quantity,
    ModifierPortion? Portion);

public sealed record ModifierGroupCompositionInput(
    Guid Id,
    string Name,
    int MinimumSelection,
    int MaximumSelection,
    ModifierPricingStrategy PricingStrategy,
    bool RequiresCompleteComposition,
    IReadOnlyList<ModifierOptionSelectionInput> Selections,
    IReadOnlyList<ModifierCompatibilityRuleInput> CompatibilityRules);

public sealed record ModifierOptionSnapshot(
    Guid GroupId,
    Guid OptionId,
    string Name,
    Money UnitPrice,
    Quantity Quantity,
    ModifierPortion? Portion);

public sealed record ModifierGroupSnapshot(
    Guid GroupId,
    string Name,
    ModifierPricingStrategy PricingStrategy,
    Money Price,
    IReadOnlyList<ModifierOptionSnapshot> Options);

public sealed record ModifierCompositionPrice(Money BasePrice, Money UnitPrice, IReadOnlyList<ModifierGroupSnapshot> Groups);

/// <summary>Validates and prices a server-resolved product composition.</summary>
public static class ModifierCompositionCalculator
{
    public static ModifierCompositionPrice Calculate(Money productBasePrice, IReadOnlyCollection<ModifierGroupCompositionInput> groups)
    {
        if (groups.Select(x => x.Id).Distinct().Count() != groups.Count)
            throw new DomainException("Product modifier groups cannot be duplicated.");
        var groupsById = groups.ToDictionary(x => x.Id);

        var compositionGroups = groups.Where(x => IsCompositionPricing(x.PricingStrategy)).ToArray();
        if (compositionGroups.Length > 1)
            throw new DomainException("A product can have only one composition pricing group.");

        var snapshots = groups.Select(CalculateGroup).ToArray();
        var compositionPrice = compositionGroups.Length == 0
            ? productBasePrice
            : snapshots.Single(x => x.GroupId == compositionGroups[0].Id).Price;
        var unitPrice = new Money(compositionPrice.Amount + snapshots
            .Where(x => x.PricingStrategy == ModifierPricingStrategy.Additive)
            .Sum(x => x.Price.Amount));

        ValidateCompatibility(groups, groupsById);
        return new ModifierCompositionPrice(productBasePrice, unitPrice, snapshots);
    }

    private static ModifierGroupSnapshot CalculateGroup(ModifierGroupCompositionInput group)
    {
        if (group.Id == Guid.Empty || string.IsNullOrWhiteSpace(group.Name) || group.MinimumSelection < 0 || group.MaximumSelection < group.MinimumSelection)
            throw new DomainException("Modifier group snapshot is invalid.");
        if (!Enum.IsDefined(group.PricingStrategy))
            throw new DomainException("Modifier pricing strategy is invalid.");

        var selected = group.Selections;
        if (selected.Any(x => x.GroupId != group.Id || x.OptionId == Guid.Empty || string.IsNullOrWhiteSpace(x.Name)))
            throw new DomainException("Modifier selection does not belong to its group.");
        if (selected.Select(x => x.OptionId).Distinct().Count() != selected.Count)
            throw new DomainException("A modifier option cannot be selected more than once in a group.");

        var isComposite = IsCompositionPricing(group.PricingStrategy);
        var selectionCount = isComposite ? selected.Count : selected.Sum(x => x.Quantity.Value);
        if (selectionCount < group.MinimumSelection || selectionCount > group.MaximumSelection)
            throw new DomainException("Modifier selection is outside the allowed range.");

        Money price;
        if (isComposite)
        {
            if (!group.RequiresCompleteComposition || selected.Count is < 1 or > 4
                || selected.Any(x => x.Quantity.Value != 1m || x.Portion is null)
                || !ModifierPortion.SumEqualsOne(selected.Select(x => x.Portion!.Value)))
                throw new DomainException("Modifier portions must compose exactly one product unit.");

            price = group.PricingStrategy == ModifierPricingStrategy.HighestPrice
                ? new Money(selected.Max(x => x.UnitPrice.Amount))
                : ProportionalPrice(selected);
        }
        else
        {
            if (group.RequiresCompleteComposition || selected.Any(x => x.Portion is not null))
                throw new DomainException("Fractions are only valid for complete product compositions.");
            price = group.PricingStrategy switch
            {
                ModifierPricingStrategy.Additive => new Money(selected.Sum(x => x.UnitPrice.Amount * x.Quantity.Value)),
                ModifierPricingStrategy.NoPriceChange => Money.Zero,
                _ => throw new DomainException("Modifier pricing strategy is invalid.")
            };
        }

        return new ModifierGroupSnapshot(group.Id, group.Name, group.PricingStrategy, price,
            selected.Select(x => new ModifierOptionSnapshot(x.GroupId, x.OptionId, x.Name, x.UnitPrice, x.Quantity, x.Portion)).ToArray());
    }

    private static Money ProportionalPrice(IReadOnlyCollection<ModifierOptionSelectionInput> selections)
    {
        var numerator = BigInteger.Zero;
        var denominator = BigInteger.One;
        foreach (var selection in selections)
        {
            var portion = selection.Portion!.Value;
            var cents = new BigInteger(selection.UnitPrice.Amount * 100m);
            numerator = numerator * portion.Denominator + cents * portion.Numerator * denominator;
            denominator *= portion.Denominator;
        }

        var wholeCents = BigInteger.DivRem(numerator, denominator, out var remainder);
        if (remainder * 2 >= denominator) wholeCents++;
        return new Money((decimal)wholeCents / 100m);
    }

    private static void ValidateCompatibility(
        IReadOnlyCollection<ModifierGroupCompositionInput> groups,
        IReadOnlyDictionary<Guid, ModifierGroupCompositionInput> groupsById)
    {
        var selected = groups.SelectMany(x => x.Selections).Select(x => (x.GroupId, x.OptionId)).ToHashSet();
        foreach (var group in groups)
        foreach (var rule in group.CompatibilityRules)
        {
            if (rule.SourceGroupId != group.Id || !Enum.IsDefined(rule.Kind)
                || !groupsById.ContainsKey(rule.TargetGroupId))
                throw new DomainException("Modifier compatibility rule references an invalid group.");

            var sourceSelected = selected.Contains((rule.SourceGroupId, rule.SourceOptionId));
            var targetSelected = selected.Contains((rule.TargetGroupId, rule.TargetOptionId));
            if (rule.Kind == ModifierCompatibilityKind.Requires && sourceSelected && !targetSelected)
                throw new DomainException("A required modifier option was not selected.");
            if (rule.Kind == ModifierCompatibilityKind.Excludes && sourceSelected && targetSelected)
                throw new DomainException("Mutually exclusive modifier options cannot be selected together.");
        }
    }

    private static bool IsCompositionPricing(ModifierPricingStrategy strategy) =>
        strategy is ModifierPricingStrategy.HighestPrice or ModifierPricingStrategy.Proportional;
}
