using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Reviews;
using ZFome.Application.Tenancy;
using ZFome.Application.Abstractions.Reviews;
using ZFome.Application.Identity;
using ZFome.Contracts.Reviews;
using ZFome.Domain.Reviews;

namespace ZFome.Api.PublicOrdering;

internal static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var publicGroup = endpoints.MapGroup("/api/public").AllowAnonymous().WithTags("Reviews");
        publicGroup.MapGet("/ordering/orders/{reference}/reviews", ProgressAsync).Produces<ReviewEligibility>().ProducesProblem(404);
        publicGroup.MapPost("/ordering/orders/{reference}/reviews", PublishAsync).RequireRateLimiting("public-review-submissions").Produces<ReviewSubmissionResult>().ProducesValidationProblem().ProducesProblem(404).ProducesProblem(409).ProducesProblem(429);
        publicGroup.MapGet("/establishments/{slug}/reviews", PublicListAsync).Produces<PublicReviewPage>().ProducesProblem(404);
        publicGroup.MapGet("/establishments/{slug}/rating-summary", SummaryAsync).Produces<ReviewRatingSummary>().ProducesProblem(404);

        var admin = endpoints.MapGroup("/api/admin/establishments/{establishmentId:guid}/reviews")
            .RequireAuthorization(AdministrativePolicies.Management).WithTags("Reviews");
        admin.MapGet("", AdminListAsync).Produces<ReviewAdminPageResponse>().ProducesProblem(403);
        admin.MapPost("/{reviewId:guid}/approve", ApproveAsync).Produces(StatusCodes.Status204NoContent).ProducesProblem(403).ProducesProblem(404);
        admin.MapPost("/{reviewId:guid}/reject", RejectAsync).Produces(StatusCodes.Status204NoContent).ProducesValidationProblem().ProducesProblem(403).ProducesProblem(404);
        admin.MapPost("/{reviewId:guid}/hide", HideAsync).Produces(StatusCodes.Status204NoContent).ProducesValidationProblem().ProducesProblem(403).ProducesProblem(404);
        admin.MapPost("/{reviewId:guid}/restore", RestoreAsync).Produces(StatusCodes.Status204NoContent).ProducesProblem(403).ProducesProblem(404);
        return endpoints;
    }

    private static async Task<IResult> ProgressAsync(string reference, IQueryDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.DispatchAsync<GetReviewProgressQuery, ReviewEligibility>(new(reference), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> PublishAsync(string reference, PublishReviewRequest request, HttpRequest http,
        ICommandDispatcher dispatcher, CancellationToken ct)
    {
        var key = http.Headers["Idempotency-Key"].ToString();
        var command = new PublishReviewsCommand(reference, key, request.EstablishmentRating, request.EstablishmentComment,
            request.Products?.Select(x => new ReviewProductInput(x.ProductId, x.Rating, x.Comment)).ToArray() ?? []);
        var result = await dispatcher.DispatchAsync<PublishReviewsCommand, ReviewSubmissionResult>(command, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> PublicListAsync(string slug, Guid? productId, int? page, int? pageSize,
        IQueryDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.DispatchAsync<ListPublicReviewsQuery, PublicReviewPage>(new(slug, productId, page ?? 1, pageSize ?? 20), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SummaryAsync(string slug, Guid? productId, IQueryDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.DispatchAsync<GetPublicReviewSummaryQuery, ReviewRatingSummary?>(new(slug, productId), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> AdminListAsync(Guid establishmentId, int? rating, ReviewStatus? status,
        DateTimeOffset? from, DateTimeOffset? to, int? page, int? pageSize, EstablishmentScopeResolver scope,
        IQueryDispatcher dispatcher, CancellationToken ct)
    {
        var actor = await scope.ResolveAsync(establishmentId, ct);
        var result = await dispatcher.DispatchAsync<ListAdminReviewsQuery, ReviewAdminPage>(
            new(actor.TenantId, actor.EstablishmentId, rating, status, from, to, page ?? 1, pageSize ?? 20), ct);
        return Results.Ok(new ReviewAdminPageResponse(result.Total, result.Items.Select(item =>
            new ReviewAdminItemResponse(item.Id, item.ProductId, item.ProductName, item.Rating, item.Comment,
                item.Status.ToString(), item.ModerationReason, item.CreatedAt, item.UpdatedAt)).ToArray()));
    }

    private static Task<IResult> HideAsync(Guid establishmentId, Guid reviewId, ModerateReviewRequest request,
        EstablishmentScopeResolver scope, ICommandDispatcher dispatcher, CancellationToken ct) =>
        ModerateAsync(establishmentId, reviewId, request.Reason, ReviewModerationAction.Hidden, scope, dispatcher, ct);

    private static Task<IResult> RejectAsync(Guid establishmentId, Guid reviewId, ModerateReviewRequest request,
        EstablishmentScopeResolver scope, ICommandDispatcher dispatcher, CancellationToken ct) =>
        ModerateAsync(establishmentId, reviewId, request.Reason, ReviewModerationAction.Rejected, scope, dispatcher, ct);

    private static Task<IResult> ApproveAsync(Guid establishmentId, Guid reviewId,
        EstablishmentScopeResolver scope, ICommandDispatcher dispatcher, CancellationToken ct) =>
        ModerateAsync(establishmentId, reviewId, null, ReviewModerationAction.Approved, scope, dispatcher, ct);

    private static Task<IResult> RestoreAsync(Guid establishmentId, Guid reviewId,
        EstablishmentScopeResolver scope, ICommandDispatcher dispatcher, CancellationToken ct) =>
        ModerateAsync(establishmentId, reviewId, null, ReviewModerationAction.Restored, scope, dispatcher, ct);

    private static async Task<IResult> ModerateAsync(Guid establishmentId, Guid reviewId, string? reason, ReviewModerationAction action,
        EstablishmentScopeResolver scope, ICommandDispatcher dispatcher, CancellationToken ct)
    {
        var actor = await scope.ResolveAsync(establishmentId, ct);
        await dispatcher.DispatchAsync(new ModerateReviewCommand(actor.TenantId, actor.EstablishmentId, actor.UserId, reviewId, reason, action), ct);
        return Results.NoContent();
    }
}

