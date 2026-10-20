namespace OrderHub.Contracts.Reviews;

public sealed record PublishReviewRequest(int EstablishmentRating, string? EstablishmentComment, IReadOnlyList<ReviewProductRequest> Products);
public sealed record ReviewProductRequest(Guid ProductId, int Rating, string? Comment);
public sealed record ModerateReviewRequest(string Reason);
