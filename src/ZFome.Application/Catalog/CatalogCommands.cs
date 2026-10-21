using ZFome.Application.Abstractions.Commands;
using ZFome.Domain.Catalog;
using ZFome.Domain.SharedKernel;

namespace ZFome.Application.Catalog;

public sealed record UpsertCategoryCommand(Guid EstablishmentId, Guid? Id, string Name, string? Description, int Order, string? ImageUrl, Guid? ParentId, bool IsActive) : ICommand<Guid>;
public sealed record ProductImageInput(string Url, int Order, bool IsPrincipal);
public sealed record ProductVariationInput(string Name, decimal Price, int Order, bool IsActive);
public sealed record ProductGroupInput(Guid GroupId, int Order);
public sealed record UpsertProductCommand(Guid EstablishmentId, Guid? Id, Guid CategoryId, string Code, string Name, string? Description, decimal BasePrice, bool IsFeatured, bool IsActive, bool AllowsNotes, IReadOnlyList<ProductImageInput> Images, IReadOnlyList<ProductVariationInput> Variations, IReadOnlyList<ProductGroupInput> AdditionalGroups) : ICommand<Guid>;
public sealed record UpsertAdditionalCommand(Guid EstablishmentId, Guid? Id, string Name, decimal Price, bool IsActive) : ICommand<Guid>;
public sealed record AdditionalGroupCompatibilityInput(Guid TargetGroupId, Guid TargetAdditionalId, ModifierCompatibilityKind Kind);
public sealed record AdditionalGroupItemInput(Guid AdditionalId, int Order, IReadOnlyList<AdditionalGroupCompatibilityInput>? CompatibilityRules = null);
public sealed record UpsertAdditionalGroupCommand(Guid EstablishmentId, Guid? Id, string Name, int MinimumSelection, int MaximumSelection, bool IsActive, IReadOnlyList<AdditionalGroupItemInput> Items, ModifierPricingStrategy PricingStrategy = ModifierPricingStrategy.Additive, ModifierGroupType Type = ModifierGroupType.Additional, bool RequiresCompleteComposition = false) : ICommand<Guid>;
