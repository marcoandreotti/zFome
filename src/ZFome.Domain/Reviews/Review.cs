using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Reviews;

public enum ReviewTargetKind { Establishment, Product }
public enum ReviewStatus { Pending, Published, Hidden, Rejected }

public readonly record struct Rating
{
    private Rating(int value) => Value = value;

    public int Value { get; }
    public bool RequiresComment => Value < 3;

    public static Rating Create(int value) => value is >= 1 and <= 5
        ? new Rating(value)
        : throw new DomainException("Rating must be an integer from 1 to 5.");
}

public sealed class Review : IEstablishmentScopedEntity
{
    private Review() { }

    private Review(Guid tenantId, Guid establishmentId, Guid orderId, ReviewTargetKind targetKind,
        Guid targetId, Rating rating, string? comment, DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty || orderId == Guid.Empty || targetId == Guid.Empty)
            throw new DomainException("Review scope and target are required.");
        if (targetKind == ReviewTargetKind.Establishment && targetId != establishmentId)
            throw new DomainException("Establishment review target is invalid.");

        Id = Guid.NewGuid();
        TenantId = tenantId;
        EstablishmentId = establishmentId;
        OrderId = orderId;
        TargetKind = targetKind;
        TargetId = targetId;
        Rating = rating;
        Comment = NormalizeComment(comment, rating);
        Status = ReviewStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid OrderId { get; private set; }
    public ReviewTargetKind TargetKind { get; private set; }
    public Guid TargetId { get; private set; }
    public Rating Rating { get; private set; }
    public string? Comment { get; private set; }
    public ReviewStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string? ModerationReason { get; private set; }

    public static Review Submit(Guid tenantId, Guid establishmentId, Guid orderId,
        ReviewTargetKind targetKind, Guid targetId, int rating, string? comment, DateTimeOffset now) =>
        new(tenantId, establishmentId, orderId, targetKind, targetId, Rating.Create(rating), comment, now);

    public void Approve(DateTimeOffset now)
    {
        if (Status != ReviewStatus.Pending)
            throw new DomainException("Only a pending review can be approved.");
        Status = ReviewStatus.Published;
        UpdatedAt = now;
    }

    public void Hide(string reason, DateTimeOffset now)
    {
        if (Status != ReviewStatus.Published)
            throw new DomainException("Only a published review can be hidden.");
        var normalized = reason.Trim();
        if (normalized.Length is < 1 or > 500)
            throw new DomainException("Moderation reason is invalid.");
        Status = ReviewStatus.Hidden;
        ModerationReason = normalized;
        UpdatedAt = now;
    }

    public void Reject(string reason, DateTimeOffset now)
    {
        if (Status != ReviewStatus.Pending)
            throw new DomainException("Only a pending review can be rejected.");
        var normalized = reason.Trim();
        if (normalized.Length is < 1 or > 500)
            throw new DomainException("Review rejection reason is invalid.");
        Status = ReviewStatus.Rejected;
        ModerationReason = normalized;
        UpdatedAt = now;
    }

    public void Restore(DateTimeOffset now)
    {
        if (Status != ReviewStatus.Hidden)
            throw new DomainException("Review is not hidden.");
        Status = ReviewStatus.Published;
        ModerationReason = null;
        UpdatedAt = now;
    }

    private static string? NormalizeComment(string? comment, Rating rating)
    {
        var normalized = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (rating.RequiresComment && normalized is null)
            throw new DomainException("Comment is required for ratings below 3.");
        if (normalized is not null && (normalized.Length > 1000 || (rating.RequiresComment && normalized.Length < 10)))
            throw new DomainException("Review comment length is invalid.");
        return normalized;
    }
}

public sealed class ReviewRequest : IEstablishmentScopedEntity
{
    private ReviewRequest() { }

    private ReviewRequest(Guid tenantId, Guid establishmentId, string key, string payloadHash, string resultJson, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EstablishmentId = establishmentId;
        Key = key;
        PayloadHash = payloadHash;
        ResultJson = resultJson;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public string ResultJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static ReviewRequest Create(Guid tenantId, Guid establishmentId, string key, string payloadHash, string resultJson, DateTimeOffset now)
    {
        var normalized = key.Trim();
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty || normalized.Length is < 8 or > 100 || payloadHash.Length != 64 || string.IsNullOrWhiteSpace(resultJson))
            throw new DomainException("Review idempotency data is invalid.");
        return new(tenantId, establishmentId, normalized, payloadHash, resultJson, now);
    }
}

public enum ReviewModerationAction { Approved, Rejected, Hidden, Restored }

public sealed class ReviewModerationHistory : IEstablishmentScopedEntity
{
    private ReviewModerationHistory() { }

    private ReviewModerationHistory(Guid tenantId, Guid establishmentId, Guid reviewId, Guid actorId,
        ReviewModerationAction action, string? reason, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EstablishmentId = establishmentId;
        ReviewId = reviewId;
        ActorId = actorId;
        Action = action;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid ReviewId { get; private set; }
    public Guid ActorId { get; private set; }
    public ReviewModerationAction Action { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static ReviewModerationHistory Create(Review review, Guid actorId, ReviewModerationAction action, string? reason, DateTimeOffset now)
    {
        if (actorId == Guid.Empty || ((action is ReviewModerationAction.Hidden or ReviewModerationAction.Rejected) && string.IsNullOrWhiteSpace(reason)))
            throw new DomainException("Review moderation history is invalid.");
        return new(review.TenantId, review.EstablishmentId, review.Id, actorId, action,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), now);
    }
}

