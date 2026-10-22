using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Catalog;

public enum ModifierGroupType
{
    Additional,
    Flavor,
    Crust,
    Removal
}

public enum ModifierCompatibilityKind
{
    Requires,
    Excludes
}

public sealed record ModifierCompatibilityInput(Guid SourceAdditionalId, Guid TargetGroupId, Guid TargetAdditionalId, ModifierCompatibilityKind Kind);

/// <summary>
/// Representa um adicional de produto dentro do escopo de um único estabelecimento, podendo ser agrupado em grupos de seleção obrigatória ou opcional.
/// </summary>
public sealed class Additional : IEstablishmentScopedEntity
{
    private Additional()
    { }

    private Additional(Guid tenantId, Guid establishmentId, string name, Money price)
    { if (tenantId == Guid.Empty || establishmentId == Guid.Empty) throw new DomainException("Additional scope is required."); var value = name.Trim(); if (value.Length is < 1 or > 150) throw new DomainException("Additional name is invalid."); Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; Name = value; Price = price; IsActive = true; }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Money Price { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Cria um adicional ativo no catálogo do estabelecimento.</summary>
    public static Additional Create(Guid tenantId, Guid establishmentId, string name, Money price) => new(tenantId, establishmentId, name, price);

    public void Update(string name, Money price)
    { var value = name.Trim(); if (value.Length is < 1 or > 150) throw new DomainException("Additional name is invalid."); Name = value; Price = price; }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}

// Classe que representa um grupo de adicionais, com limites de seleção obrigatória ou opcional, dentro do escopo de um único estabelecimento.
public sealed class AdditionalGroup : IEstablishmentScopedEntity
{
    private readonly List<AdditionalGroupItem> items = [];
    private readonly List<AdditionalGroupCompatibilityRule> compatibilityRules = [];

    private AdditionalGroup()
    { }

    private AdditionalGroup(Guid tenantId, Guid establishmentId, string name, int minimum, int maximum, ModifierPricingStrategy pricingStrategy, ModifierGroupType type, bool requiresCompleteComposition)
    { if (tenantId == Guid.Empty || establishmentId == Guid.Empty || minimum < 0 || maximum < 1 || minimum > maximum) throw new DomainException("Additional-group selection range is invalid."); var value = name.Trim(); if (value.Length is < 1 or > 150) throw new DomainException("Additional-group name is invalid."); ValidatePricingConfiguration(minimum, maximum, pricingStrategy, type, requiresCompleteComposition); Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; Name = value; MinimumSelection = minimum; MaximumSelection = maximum; PricingStrategy = pricingStrategy; Type = type; RequiresCompleteComposition = requiresCompleteComposition; IsActive = true; }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int MinimumSelection { get; private set; }
    public int MaximumSelection { get; private set; }
    public ModifierPricingStrategy PricingStrategy { get; private set; }
    public ModifierGroupType Type { get; private set; }
    public bool RequiresCompleteComposition { get; private set; }
    public bool IsRequired => MinimumSelection > 0;
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<AdditionalGroupItem> Items => items;
    public IReadOnlyCollection<AdditionalGroupCompatibilityRule> CompatibilityRules => compatibilityRules;

    /// <summary>Cria um grupo de adicionais com limites válidos de seleção.</summary>
    public static AdditionalGroup Create(Guid tenantId, Guid establishmentId, string name, int minimum, int maximum, ModifierPricingStrategy pricingStrategy = ModifierPricingStrategy.Additive, ModifierGroupType type = ModifierGroupType.Additional, bool requiresCompleteComposition = false) => new(tenantId, establishmentId, name, minimum, maximum, pricingStrategy, type, requiresCompleteComposition);

    public void Update(string name, int minimum, int maximum, ModifierPricingStrategy pricingStrategy = ModifierPricingStrategy.Additive, ModifierGroupType type = ModifierGroupType.Additional, bool requiresCompleteComposition = false)
    { if (minimum < 0 || maximum < 1 || minimum > maximum) throw new DomainException("Additional-group selection range is invalid."); var value = name.Trim(); if (value.Length is < 1 or > 150) throw new DomainException("Additional-group name is invalid."); ValidatePricingConfiguration(minimum, maximum, pricingStrategy, type, requiresCompleteComposition); Name = value; MinimumSelection = minimum; MaximumSelection = maximum; PricingStrategy = pricingStrategy; Type = type; RequiresCompleteComposition = requiresCompleteComposition; }

    private static void ValidatePricingConfiguration(int minimum, int maximum, ModifierPricingStrategy strategy, ModifierGroupType type, bool requiresCompleteComposition)
    {
        if (!Enum.IsDefined(strategy) || !Enum.IsDefined(type)) throw new DomainException("Modifier group configuration is invalid.");
        var usesPortions = strategy is ModifierPricingStrategy.HighestPrice or ModifierPricingStrategy.Proportional;
        if (usesPortions && (!requiresCompleteComposition || minimum < 1 || maximum > 4))
            throw new DomainException("Composite pricing requires a complete composition with one to four selections.");
        if (!usesPortions && requiresCompleteComposition)
            throw new DomainException("Complete composition is only valid for a composite pricing strategy.");
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>Inclui um adicional do mesmo estabelecimento sem criar vínculos duplicados.</summary>
    public void AddItem(Additional additional, int order)
    { if (additional.TenantId != TenantId || additional.EstablishmentId != EstablishmentId || order < 0) throw new DomainException("Additional must belong to the same establishment."); if (items.All(item => item.AdditionalId != additional.Id)) items.Add(new AdditionalGroupItem(TenantId, EstablishmentId, Id, additional.Id, order)); }

    public void RemoveItem(Guid additionalId) => items.RemoveAll(item => item.AdditionalId == additionalId);

    /// <summary>Substitui integralmente os adicionais associados ao grupo.</summary>
    public void ReplaceItems(IEnumerable<(Additional Additional, int Order)> replacements)
    { var values = replacements.ToArray(); var retainedIds = values.Select(x => x.Additional.Id).ToHashSet(); compatibilityRules.RemoveAll(x => !retainedIds.Contains(x.SourceAdditionalId)); items.Clear(); foreach (var item in values) AddItem(item.Additional, item.Order); }

    /// <summary>Substitui requisitos direcionais e exclusões mútuas entre opções deste e de outros grupos.</summary>
    public void ReplaceCompatibilityRules(IEnumerable<ModifierCompatibilityInput> replacements)
    {
        var values = replacements.ToArray();
        if (values.Any(x => !items.Any(item => item.AdditionalId == x.SourceAdditionalId)
            || x.TargetGroupId == Guid.Empty || x.TargetAdditionalId == Guid.Empty
            || !Enum.IsDefined(x.Kind) || (x.TargetGroupId == Id && x.TargetAdditionalId == x.SourceAdditionalId)))
            throw new DomainException("Modifier compatibility rule is invalid.");
        if (values.Distinct().Count() != values.Length)
            throw new DomainException("Modifier compatibility rules cannot be duplicated.");
        compatibilityRules.Clear();
        compatibilityRules.AddRange(values.Select(x => new AdditionalGroupCompatibilityRule(TenantId, EstablishmentId, Id, x.SourceAdditionalId, x.TargetGroupId, x.TargetAdditionalId, x.Kind)));
    }

    /// <summary>Valida se a quantidade selecionada respeita os limites configurados.</summary>
    public void ValidateSelection(int selectedCount)
    { if (selectedCount < MinimumSelection || selectedCount > MaximumSelection) throw new DomainException("Additional selection is outside the allowed range."); }
}

public sealed class AdditionalGroupCompatibilityRule
{
    private AdditionalGroupCompatibilityRule() { }

    internal AdditionalGroupCompatibilityRule(Guid tenantId, Guid establishmentId, Guid sourceGroupId, Guid sourceAdditionalId, Guid targetGroupId, Guid targetAdditionalId, ModifierCompatibilityKind kind)
    { TenantId = tenantId; EstablishmentId = establishmentId; SourceGroupId = sourceGroupId; SourceAdditionalId = sourceAdditionalId; TargetGroupId = targetGroupId; TargetAdditionalId = targetAdditionalId; Kind = kind; }

    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid SourceGroupId { get; private set; }
    public Guid SourceAdditionalId { get; private set; }
    public Guid TargetGroupId { get; private set; }
    public Guid TargetAdditionalId { get; private set; }
    public ModifierCompatibilityKind Kind { get; private set; }
}

// Classe que representa a associação de um adicional a um grupo, com uma ordem de exibição, dentro do escopo de um único estabelecimento.
public sealed class AdditionalGroupItem
{
    private AdditionalGroupItem()
    { }

    internal AdditionalGroupItem(Guid tenantId, Guid establishmentId, Guid groupId, Guid additionalId, int order)
    { TenantId = tenantId; EstablishmentId = establishmentId; GroupId = groupId; AdditionalId = additionalId; Order = order; }

    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid AdditionalId { get; private set; }
    public int Order { get; private set; }
}
