using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using OrderHub.Application.Abstractions.Commands;
using OrderHub.Application.Abstractions.PublicOrdering;
using OrderHub.Application.Abstractions.Queries;
using OrderHub.Application.Abstractions.Reviews;
using OrderHub.Application.Exceptions;
using OrderHub.Domain.Ordering;
using OrderHub.Domain.Reviews;

namespace OrderHub.Application.Reviews;

public sealed record ReviewProductInput(Guid ProductId, int Rating, string? Comment);
public sealed record PublishReviewsCommand(string Reference, string IdempotencyKey, int EstablishmentRating,
    string? EstablishmentComment, IReadOnlyList<ReviewProductInput> Products) : ICommand<ReviewSubmissionResult>;
public sealed record ReviewSubmissionResult(DateTimeOffset SubmittedAt, int RatingsSubmitted);

public sealed record GetReviewProgressQuery(string Reference) : IQuery<ReviewEligibility>;
public sealed record ReviewEligibility(bool CanReview, DateTimeOffset? ExpiresAt,
    bool EstablishmentReviewed, IReadOnlyList<ReviewProgress> Products, bool PendingApproval,
    string EstablishmentName = "", string? EstablishmentLogoUrl = null, string EstablishmentSlug = "");

public sealed record ListPublicReviewsQuery(string Slug, Guid? ProductId, int Page, int PageSize) : IQuery<PublicReviewPage>;
public sealed record PublicReviewPage(long Total, IReadOnlyList<ReviewPublicItem> Items);
public sealed record GetPublicReviewSummaryQuery(string Slug, Guid? ProductId) : IQuery<ReviewRatingSummary?>;
public sealed record ListAdminReviewsQuery(Guid TenantId, Guid EstablishmentId, int? Rating,
    ReviewStatus? Status, DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize) : IQuery<ReviewAdminPage>;
public sealed record ModerateReviewCommand(Guid TenantId, Guid EstablishmentId, Guid ActorId,
    Guid ReviewId, string? Reason, ReviewModerationAction Action) : ICommand;

public sealed class PublishReviewsHandler(
    IReviewOrderGateway orders,
    IReviewWriteRepository repository,
    IReviewTransaction transaction,
    TimeProvider timeProvider) : ICommandHandler<PublishReviewsCommand, ReviewSubmissionResult>
{
    public Task<ReviewSubmissionResult> HandleAsync(PublishReviewsCommand command, CancellationToken cancellationToken) =>
        transaction.ExecuteAsync(async ct =>
        {
            var order = await orders.FindAsync(command.Reference, ct)
                ?? throw new NotFoundException("Order was not found.");
            var canonicalProducts = command.Products
                .Select(x => new ReviewProductInput(x.ProductId, x.Rating, NormalizeComment(x.Comment)))
                .OrderBy(x => x.ProductId)
                .ToArray();
            var canonical = JsonSerializer.Serialize(new
            {
                orderId = order.OrderId,
                establishmentRating = command.EstablishmentRating,
                establishmentComment = NormalizeComment(command.EstablishmentComment),
                products = canonicalProducts
            });
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            var key = command.IdempotencyKey.Trim();
            var existingRequest = await repository.FindRequestAsync(order.TenantId, order.EstablishmentId, key, ct);
            if (existingRequest is not null)
            {
                if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(existingRequest.PayloadHash), Encoding.ASCII.GetBytes(hash)))
                    throw new ConflictException("The idempotency key was already used with a different review.");
                return JsonSerializer.Deserialize<ReviewSubmissionResult>(existingRequest.ResultJson)
                    ?? throw new InvalidOperationException("Stored review response is invalid.");
            }

            var now = timeProvider.GetUtcNow();
            if (!IsEligible(order, now))
                throw new OrderHub.Domain.Exceptions.DomainException("Order is not eligible for review.");
            if (canonicalProducts.Select(x => x.ProductId).Distinct().Count() != canonicalProducts.Length)
                throw new OrderHub.Domain.Exceptions.DomainException("A product may be reviewed only once per order.");
            var purchasedProducts = order.Products.Select(x => x.ProductId).ToHashSet();
            if (canonicalProducts.Any(x => !purchasedProducts.Contains(x.ProductId)))
                throw new OrderHub.Domain.Exceptions.DomainException("Product was not part of this order.");

            var existing = await repository.FindForOrderAsync(order.TenantId, order.EstablishmentId, order.OrderId, ct);
            if (existing.Any(x => x.TargetKind == ReviewTargetKind.Establishment)
                || canonicalProducts.Any(input => existing.Any(x => x.TargetKind == ReviewTargetKind.Product && x.TargetId == input.ProductId)))
                throw new ConflictException("One or more order targets have already been reviewed.");

            var submitted = 1 + canonicalProducts.Length;
            var establishmentReview = Review.Submit(order.TenantId, order.EstablishmentId, order.OrderId,
                ReviewTargetKind.Establishment, order.EstablishmentId, command.EstablishmentRating,
                NormalizeComment(command.EstablishmentComment), now);
            await repository.AddAsync(establishmentReview, ct);

            foreach (var product in canonicalProducts)
            {
                var review = Review.Submit(order.TenantId, order.EstablishmentId, order.OrderId,
                    ReviewTargetKind.Product, product.ProductId, product.Rating, product.Comment, now);
                await repository.AddAsync(review, ct);
            }

            var result = new ReviewSubmissionResult(now, submitted);
            var serialized = JsonSerializer.Serialize(result);
            await repository.AddRequestAsync(ReviewRequest.Create(order.TenantId, order.EstablishmentId,
                key, hash, serialized, now), ct);
            await repository.SaveChangesAsync(ct);
            return result;
        }, cancellationToken);

    private static bool IsEligible(ReviewOrderContext order, DateTimeOffset now) =>
        string.Equals(order.Status, OrderStatus.Completed.ToString(), StringComparison.Ordinal)
        && order.CompletedAt is { } completedAt
        && now <= completedAt.AddDays(30);

    private static string? NormalizeComment(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class PublishReviewsValidator : AbstractValidator<PublishReviewsCommand>
{
    public PublishReviewsValidator()
    {
        RuleFor(x => x.Reference).Matches("^[a-f0-9]{48}$");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleFor(x => x.EstablishmentRating).InclusiveBetween(1, 5);
        RuleFor(x => x.EstablishmentComment).Must((command, comment) => IsCommentValid(command.EstablishmentRating, comment));
        RuleFor(x => x.Products).NotNull();
        RuleForEach(x => x.Products).ChildRules(product =>
        {
            product.RuleFor(x => x.ProductId).NotEmpty();
            product.RuleFor(x => x.Rating).InclusiveBetween(1, 5);
            product.RuleFor(x => x.Comment).Must((input, comment) => IsCommentValid(input.Rating, comment));
        });
    }

    private static bool IsCommentValid(int rating, string? comment)
    {
        var normalized = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        return normalized is null
            ? rating >= 3
            : normalized.Length <= 1000 && (rating >= 3 || normalized.Length >= 10);
    }
}

public sealed class GetReviewProgressHandler(IReviewOrderGateway orders, IReviewReadGateway reviews, TimeProvider timeProvider)
    : IQueryHandler<GetReviewProgressQuery, ReviewEligibility>
{
    public async Task<ReviewEligibility> HandleAsync(GetReviewProgressQuery query, CancellationToken cancellationToken)
    {
        var order = await orders.FindAsync(query.Reference, cancellationToken)
            ?? throw new NotFoundException("Order was not found.");
        var progress = await reviews.GetProgressAsync(order, cancellationToken);
        var canReview = IsEligible(order, timeProvider.GetUtcNow()) && !progress.EstablishmentReviewed;
        return new(canReview, order.CompletedAt?.AddDays(30), progress.EstablishmentReviewed,
            progress.Products, progress.PendingApproval, order.EstablishmentName, order.EstablishmentLogoUrl,
            order.EstablishmentSlug);
    }

    internal static bool IsEligible(ReviewOrderContext order, DateTimeOffset now) =>
        string.Equals(order.Status, OrderStatus.Completed.ToString(), StringComparison.Ordinal)
        && order.CompletedAt is { } completedAt
        && now <= completedAt.AddDays(30);
}

public sealed class ListPublicReviewsHandler(IPublicOrderingContextGateway contexts, IReviewReadGateway reviews)
    : IQueryHandler<ListPublicReviewsQuery, PublicReviewPage>
{
    public async Task<PublicReviewPage> HandleAsync(ListPublicReviewsQuery query, CancellationToken cancellationToken)
    {
        var context = await contexts.ResolveAsync(query.Slug.Trim().ToLowerInvariant(), null, cancellationToken)
            ?? throw new NotFoundException("Public ordering context was not found.");
        var result = await reviews.ListPublicAsync(context.TenantId, context.EstablishmentId,
            query.ProductId, query.Page, query.PageSize, cancellationToken);
        return new(result.Total, result.Items);
    }
}

public sealed class ListPublicReviewsValidator : AbstractValidator<ListPublicReviewsQuery>
{
    public ListPublicReviewsValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ProductId).Must(x => x is null || x != Guid.Empty);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetPublicReviewSummaryHandler(IPublicOrderingContextGateway contexts, IReviewReadGateway reviews)
    : IQueryHandler<GetPublicReviewSummaryQuery, ReviewRatingSummary?>
{
    public async Task<ReviewRatingSummary?> HandleAsync(GetPublicReviewSummaryQuery query, CancellationToken cancellationToken)
    {
        var context = await contexts.ResolveAsync(query.Slug.Trim().ToLowerInvariant(), null, cancellationToken)
            ?? throw new NotFoundException("Public ordering context was not found.");
        var targetKind = query.ProductId.HasValue ? ReviewTargetKind.Product : ReviewTargetKind.Establishment;
        var targetId = query.ProductId ?? context.EstablishmentId;
        return await reviews.GetSummaryAsync(context.TenantId, context.EstablishmentId, targetKind, targetId, cancellationToken);
    }
}

public sealed class ListAdminReviewsHandler(IReviewReadGateway reviews)
    : IQueryHandler<ListAdminReviewsQuery, ReviewAdminPage>
{
    public Task<ReviewAdminPage> HandleAsync(ListAdminReviewsQuery query, CancellationToken cancellationToken) =>
        reviews.ListAdminAsync(query.TenantId, query.EstablishmentId, query.Rating, query.Status,
            query.From, query.To, query.Page, query.PageSize, cancellationToken);
}

public sealed class ModerateReviewHandler(IReviewWriteRepository repository, IReviewTransaction transaction,
    TimeProvider timeProvider) : ICommandHandler<ModerateReviewCommand>
{
    public async Task HandleAsync(ModerateReviewCommand command, CancellationToken cancellationToken)
    {
        await transaction.ExecuteAsync(async ct =>
        {
            var review = await repository.GetAsync(command.TenantId, command.EstablishmentId, command.ReviewId, ct)
                ?? throw new NotFoundException("Review was not found.");
            var now = timeProvider.GetUtcNow();
            if (command.Action == ReviewModerationAction.Approved)
            {
                review.Approve(now);
                await repository.ApplyRatingDeltaAsync(command.TenantId, command.EstablishmentId,
                    review.TargetKind, review.TargetId, review.Rating.Value, 1, now, ct);
                await repository.AddModerationHistoryAsync(ReviewModerationHistory.Create(review, command.ActorId,
                    ReviewModerationAction.Approved, null, now), ct);
            }
            else if (command.Action == ReviewModerationAction.Rejected)
            {
                review.Reject(command.Reason ?? string.Empty, now);
                await repository.AddModerationHistoryAsync(ReviewModerationHistory.Create(review, command.ActorId,
                    ReviewModerationAction.Rejected, command.Reason, now), ct);
            }
            else if (command.Action == ReviewModerationAction.Restored)
            {
                review.Restore(now);
                await repository.ApplyRatingDeltaAsync(command.TenantId, command.EstablishmentId,
                    review.TargetKind, review.TargetId, review.Rating.Value, 1, now, ct);
                await repository.AddModerationHistoryAsync(ReviewModerationHistory.Create(review, command.ActorId,
                    ReviewModerationAction.Restored, null, now), ct);
            }
            else
            {
                review.Hide(command.Reason ?? string.Empty, now);
                await repository.ApplyRatingDeltaAsync(command.TenantId, command.EstablishmentId,
                    review.TargetKind, review.TargetId, review.Rating.Value, -1, now, ct);
                await repository.AddModerationHistoryAsync(ReviewModerationHistory.Create(review, command.ActorId,
                    ReviewModerationAction.Hidden, command.Reason, now), ct);
            }
            await repository.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }
}

public sealed class ModerateReviewValidator : AbstractValidator<ModerateReviewCommand>
{
    public ModerateReviewValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.EstablishmentId).NotEmpty();
        RuleFor(x => x.ActorId).NotEmpty();
        RuleFor(x => x.ReviewId).NotEmpty();
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.Reason).Must(x => x is not null && x.Trim().Length is >= 1 and <= 500)
            .When(x => x.Action is ReviewModerationAction.Hidden or ReviewModerationAction.Rejected);
    }
}

public sealed class ListAdminReviewsValidator : AbstractValidator<ListAdminReviewsQuery>
{
    public ListAdminReviewsValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.EstablishmentId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5).When(x => x.Rating.HasValue);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From.HasValue && x.To.HasValue);
    }
}

