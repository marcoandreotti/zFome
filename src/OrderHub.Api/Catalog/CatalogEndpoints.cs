using OrderHub.Application.Abstractions.Catalog;
using OrderHub.Application.Abstractions.Commands;
using OrderHub.Application.Abstractions.Queries;
using OrderHub.Application.Catalog;
using OrderHub.Application.Identity;
using OrderHub.Contracts.Catalog;
using OrderHub.Contracts.Administration;
using OrderHub.Application.Availability;
using OrderHub.Domain.Catalog;
using OrderHub.Domain.SharedKernel;

namespace OrderHub.Api.Catalog;

internal static class CatalogEndpoints
{
    private static async Task<IResult> SearchAdditionalsAsync(Guid establishmentId, string? search, bool? isActive, int? page, int? pageSize, IQueryDispatcher dispatcher, CancellationToken ct)
    {
        var query = new SearchAdditionalsQuery(establishmentId, search, isActive, page ?? 1, pageSize ?? 20);
        var result = await dispatcher.DispatchAsync<SearchAdditionalsQuery, AdditionalSearchResult>(query, ct);
        return Results.Ok(new PagedResponse<AdditionalResponse>(query.Page, query.PageSize, result.Total,
            result.Items.Select(x => new AdditionalResponse(x.Id, x.Name, x.Price, x.IsActive, x.Order)).ToArray()));
    }

    private static async Task<IResult> SearchGroupsAsync(Guid establishmentId, string? search, bool? isActive, int? page, int? pageSize, IQueryDispatcher dispatcher, CancellationToken ct)
    {
        var query = new SearchAdditionalGroupsQuery(establishmentId, search, isActive, page ?? 1, pageSize ?? 20);
        var result = await dispatcher.DispatchAsync<SearchAdditionalGroupsQuery, AdditionalGroupSearchResult>(query, ct);
        return Results.Ok(new PagedResponse<AdditionalGroupResponse>(query.Page, query.PageSize, result.Total,
            result.Items.Select(MapGroup).ToArray()));
    }

    /// <summary>Registra os endpoints administrativos e públicos do catálogo.</summary>
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/establishments/{establishmentId:guid}/catalog").RequireAuthorization(AdministrativePolicies.Management).WithTags("Administration - Catalog");
        admin.MapGet("/", GetAdministrativeAsync);
        admin.MapGet("/additionals", SearchAdditionalsAsync);
        admin.MapGet("/additional-groups", SearchGroupsAsync);
        admin.MapPost("/categories", (Guid establishmentId, UpsertCategoryRequest request, ICommandDispatcher dispatcher, CancellationToken ct) => UpsertCategoryAsync(establishmentId, null, request, dispatcher, ct));
        admin.MapPut("/categories/{id:guid}", (Guid establishmentId, Guid id, UpsertCategoryRequest request, ICommandDispatcher dispatcher, CancellationToken ct) => UpsertCategoryAsync(establishmentId, id, request, dispatcher, ct));
        admin.MapPost("/products", (Guid establishmentId, UpsertProductRequest request, ICommandDispatcher dispatcher, CancellationToken ct) => UpsertProductAsync(establishmentId, null, request, dispatcher, ct));
        admin.MapPut("/products/{id:guid}", (Guid establishmentId, Guid id, UpsertProductRequest request, ICommandDispatcher dispatcher, CancellationToken ct) => UpsertProductAsync(establishmentId, id, request, dispatcher, ct));
        admin.MapPost("/additionals", (Guid establishmentId, UpsertAdditionalRequest request, ICommandDispatcher dispatcher, CancellationToken ct) => UpsertAdditionalAsync(establishmentId, null, request, dispatcher, ct));
        admin.MapPut("/additionals/{id:guid}", (Guid establishmentId, Guid id, UpsertAdditionalRequest request, ICommandDispatcher dispatcher, CancellationToken ct) => UpsertAdditionalAsync(establishmentId, id, request, dispatcher, ct));
        admin.MapPost("/additional-groups", (Guid establishmentId, UpsertAdditionalGroupRequest request, ICommandDispatcher dispatcher, CancellationToken ct) => UpsertGroupAsync(establishmentId, null, request, dispatcher, ct));
        admin.MapPut("/additional-groups/{id:guid}", (Guid establishmentId, Guid id, UpsertAdditionalGroupRequest request, ICommandDispatcher dispatcher, CancellationToken ct) => UpsertGroupAsync(establishmentId, id, request, dispatcher, ct));
        admin.MapPut("/offers/{kind}/{offerId:guid}/unavailability", async (Guid establishmentId, OfferKind kind, Guid offerId, OfferUnavailabilityRequest request, ICommandDispatcher dispatcher, CancellationToken ct) =>
        { await dispatcher.DispatchAsync(new SetOfferUnavailabilityCommand(establishmentId, kind, offerId, request.EndsAt, request.Reason), ct); return Results.NoContent(); });
        admin.MapDelete("/offers/{kind}/{offerId:guid}/unavailability", async (Guid establishmentId, OfferKind kind, Guid offerId, ICommandDispatcher dispatcher, CancellationToken ct) =>
        { await dispatcher.DispatchAsync(new ReactivateOfferCommand(establishmentId, kind, offerId), ct); return Results.NoContent(); });
        endpoints.MapGet("/api/public/establishments/{slug}/catalog", GetPublicAsync).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> GetAdministrativeAsync(Guid establishmentId, IQueryDispatcher dispatcher, CancellationToken ct) => Results.Ok(Map(await dispatcher.DispatchAsync<GetAdministrativeCatalogQuery, CatalogReadModel>(new(establishmentId), ct)));
    private static async Task<IResult> GetPublicAsync(string slug, IQueryDispatcher dispatcher, CancellationToken ct) => Results.Ok(Map(await dispatcher.DispatchAsync<GetPublicCatalogQuery, CatalogReadModel>(new(slug), ct)));
    private static async Task<IResult> UpsertCategoryAsync(Guid establishmentId, Guid? id, UpsertCategoryRequest r, ICommandDispatcher d, CancellationToken ct) => Results.Ok(new { id = await d.DispatchAsync<UpsertCategoryCommand, Guid>(new(establishmentId, id, r.Name, r.Description, r.Order, r.ImageUrl, r.ParentId, r.IsActive), ct) });
    private static async Task<IResult> UpsertProductAsync(Guid establishmentId, Guid? id, UpsertProductRequest r, ICommandDispatcher d, CancellationToken ct) => Results.Ok(new { id = await d.DispatchAsync<UpsertProductCommand, Guid>(new(establishmentId, id, r.CategoryId, r.Code, r.Name, r.Description, r.BasePrice, r.IsFeatured, r.IsActive, r.AllowsNotes, r.Images.Select(x => new ProductImageInput(x.Url, x.Order, x.IsPrincipal)).ToList(), r.Variations.Select(x => new ProductVariationInput(x.Name, x.Price, x.Order, x.IsActive)).ToList(), r.AdditionalGroups.Select(x => new ProductGroupInput(x.GroupId, x.Order)).ToList()), ct) });
    private static async Task<IResult> UpsertAdditionalAsync(Guid establishmentId, Guid? id, UpsertAdditionalRequest r, ICommandDispatcher d, CancellationToken ct) => Results.Ok(new { id = await d.DispatchAsync<UpsertAdditionalCommand, Guid>(new(establishmentId, id, r.Name, r.Price, r.IsActive), ct) });
    private static async Task<IResult> UpsertGroupAsync(Guid establishmentId, Guid? id, UpsertAdditionalGroupRequest r, ICommandDispatcher d, CancellationToken ct) => Results.Ok(new { id = await d.DispatchAsync<UpsertAdditionalGroupCommand, Guid>(new(establishmentId, id, r.Name, r.MinimumSelection, r.MaximumSelection, r.IsActive, r.Items.Select(x => new AdditionalGroupItemInput(x.AdditionalId, x.Order, x.CompatibilityRules?.Select(rule => new AdditionalGroupCompatibilityInput(rule.TargetGroupId, rule.TargetAdditionalId, Enum.Parse<ModifierCompatibilityKind>(rule.Kind.ToString()))).ToArray())).ToArray(), Enum.Parse<ModifierPricingStrategy>(r.PricingStrategy.ToString()), Enum.Parse<ModifierGroupType>(r.Type.ToString()), r.RequiresCompleteComposition), ct) });
    private static AdditionalGroupResponse MapGroup(AdditionalGroupReadModel group) => new(group.Id, group.Name, group.MinimumSelection, group.MaximumSelection, group.IsActive, group.Order,
        group.Items.Select(MapAdditional).ToArray(), Enum.Parse<ModifierPricingStrategyContract>(group.PricingStrategy), Enum.Parse<ModifierGroupTypeContract>(group.Type), group.RequiresCompleteComposition);
    private static AdditionalResponse MapAdditional(AdditionalReadModel item) => new(item.Id, item.Name, item.Price, item.IsActive, item.Order, item.IsAvailable, item.UnavailabilityReason, item.AvailableAgainAt,
        item.CompatibilityRules?.Select(rule => new AdditionalGroupCompatibilityRuleResponse(rule.TargetGroupId, rule.TargetAdditionalId, Enum.Parse<ModifierCompatibilityKindContract>(rule.Kind))).ToArray());
    private static CatalogResponse Map(CatalogReadModel m) => new(m.EstablishmentId, m.EstablishmentName, m.Slug, m.Categories.Select(c => new CategoryResponse(c.Id, c.ParentId, c.Name, c.Description, c.Order, c.ImageUrl, c.IsActive, c.Products.Select(p => new ProductResponse(p.Id, p.Code, p.Name, p.Description, p.BasePrice, p.IsFeatured, p.IsActive, p.AllowsNotes, p.Images.Select(i => new ProductImageResponse(i.Id, i.Url, i.Order, i.IsPrincipal)).ToList(), p.Variations.Select(v => new ProductVariationResponse(v.Id, v.Name, v.Price, v.Order, v.IsActive, v.IsAvailable, v.UnavailabilityReason, v.AvailableAgainAt)).ToList(), p.AdditionalGroups.Select(MapGroup).ToList(), p.IsAvailable, p.UnavailabilityReason, p.AvailableAgainAt, p.RatingAverage, p.RatingCount)).ToList())).ToList());
}
