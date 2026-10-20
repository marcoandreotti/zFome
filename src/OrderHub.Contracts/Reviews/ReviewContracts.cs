namespace OrderHub.Contracts.Reviews;

public sealed record PublishReviewRequest(int EstablishmentRating, string? EstablishmentComment, IReadOnlyList<ReviewProductRequest> Products);
public sealed record ReviewProductRequest(Guid ProductId, int Rating, string? Comment);
public sealed record ModerateReviewRequest(string Reason);
public sealed record ReviewAdminItemResponse(Guid Id, Guid? ProductId, string? ProductName, int Rating,
    string? Comment, string Status, string? ModerationReason, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ReviewAdminPageResponse(long Total, IReadOnlyList<ReviewAdminItemResponse> Items);
