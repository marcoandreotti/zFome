using System.Text.Json.Serialization;

namespace OrderHub.Contracts.Catalog;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModifierPricingStrategyContract { Additive, HighestPrice, Proportional, NoPriceChange }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModifierGroupTypeContract { Additional, Flavor, Crust, Removal }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModifierCompatibilityKindContract { Requires, Excludes }

public sealed record UpsertCategoryRequest(string Name, string? Description, int Order, string? ImageUrl, Guid? ParentId, bool IsActive);
public sealed record ProductImageRequest(string Url, int Order, bool IsPrincipal);
public sealed record ProductVariationRequest(string Name, decimal Price, int Order, bool IsActive);
public sealed record ProductGroupRequest(Guid GroupId, int Order);
public sealed record UpsertProductRequest(Guid CategoryId, string Code, string Name, string? Description, decimal BasePrice, bool IsFeatured, bool IsActive, bool AllowsNotes, IReadOnlyList<ProductImageRequest> Images, IReadOnlyList<ProductVariationRequest> Variations, IReadOnlyList<ProductGroupRequest> AdditionalGroups);
public sealed record UpsertAdditionalRequest(string Name, decimal Price, bool IsActive);
public sealed record AdditionalGroupCompatibilityRuleRequest(Guid TargetGroupId, Guid TargetAdditionalId, ModifierCompatibilityKindContract Kind);
public sealed record AdditionalGroupItemRequest(Guid AdditionalId, int Order, IReadOnlyList<AdditionalGroupCompatibilityRuleRequest>? CompatibilityRules = null);
public sealed record UpsertAdditionalGroupRequest(string Name, int MinimumSelection, int MaximumSelection, bool IsActive, IReadOnlyList<AdditionalGroupItemRequest> Items, ModifierPricingStrategyContract PricingStrategy = ModifierPricingStrategyContract.Additive, ModifierGroupTypeContract Type = ModifierGroupTypeContract.Additional, bool RequiresCompleteComposition = false);
public sealed record CatalogResponse(Guid EstablishmentId, string EstablishmentName, string Slug, IReadOnlyList<CategoryResponse> Categories);
public sealed record CategoryResponse(Guid Id, Guid? ParentId, string Name, string? Description, int Order, string? ImageUrl, bool IsActive, IReadOnlyList<ProductResponse> Products);
public sealed record ProductResponse(Guid Id, string Code, string Name, string? Description, decimal BasePrice, bool IsFeatured, bool IsActive, bool AllowsNotes, IReadOnlyList<ProductImageResponse> Images, IReadOnlyList<ProductVariationResponse> Variations, IReadOnlyList<AdditionalGroupResponse> AdditionalGroups, bool IsAvailable = true, string? UnavailabilityReason = null, DateTimeOffset? AvailableAgainAt = null, decimal? RatingAverage = null, long RatingCount = 0);
public sealed record ProductImageResponse(Guid Id, string Url, int Order, bool IsPrincipal);
public sealed record ProductVariationResponse(Guid Id, string Name, decimal Price, int Order, bool IsActive, bool IsAvailable = true, string? UnavailabilityReason = null, DateTimeOffset? AvailableAgainAt = null);
public sealed record AdditionalGroupResponse(Guid Id, string Name, int MinimumSelection, int MaximumSelection, bool IsActive, int Order, IReadOnlyList<AdditionalResponse> Items, ModifierPricingStrategyContract PricingStrategy = ModifierPricingStrategyContract.Additive, ModifierGroupTypeContract Type = ModifierGroupTypeContract.Additional, bool RequiresCompleteComposition = false);
public sealed record AdditionalResponse(Guid Id, string Name, decimal Price, bool IsActive, int Order, bool IsAvailable = true, string? UnavailabilityReason = null, DateTimeOffset? AvailableAgainAt = null, IReadOnlyList<AdditionalGroupCompatibilityRuleResponse>? CompatibilityRules = null);
public sealed record AdditionalGroupCompatibilityRuleResponse(Guid TargetGroupId, Guid TargetAdditionalId, ModifierCompatibilityKindContract Kind);
