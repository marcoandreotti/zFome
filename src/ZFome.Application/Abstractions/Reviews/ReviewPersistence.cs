using ZFome.Domain.Reviews;

namespace ZFome.Application.Abstractions.Reviews;

public interface IReviewOrderGateway
{
    Task<ReviewOrderContext?> FindAsync(string publicReference, CancellationToken cancellationToken);
}

public interface IReviewWriteRepository
{
    Task<ReviewRequest?> FindRequestAsync(Guid tenantId, Guid establishmentId, string key, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Review>> FindForOrderAsync(Guid tenantId, Guid establishmentId, Guid orderId, CancellationToken cancellationToken);
    Task<Review?> GetAsync(Guid tenantId, Guid establishmentId, Guid reviewId, CancellationToken cancellationToken);
    Task AddAsync(Review review, CancellationToken cancellationToken);
    Task AddRequestAsync(ReviewRequest request, CancellationToken cancellationToken);
    Task AddModerationHistoryAsync(ReviewModerationHistory history, CancellationToken cancellationToken);
    Task ApplyRatingDeltaAsync(Guid tenantId, Guid establishmentId, ReviewTargetKind targetKind, Guid targetId, int rating, int delta, DateTimeOffset now, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IReviewTransaction
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}

public interface IReviewReadGateway
{
    Task<ReviewPublicPage> ListPublicAsync(Guid tenantId, Guid establishmentId, Guid? productId, int page, int pageSize, CancellationToken cancellationToken);
    Task<ReviewRatingSummary?> GetSummaryAsync(Guid tenantId, Guid establishmentId, ReviewTargetKind targetKind, Guid targetId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewRatingSummary>> GetProductSummariesAsync(Guid tenantId, Guid establishmentId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
    Task<ReviewAdminPage> ListAdminAsync(Guid tenantId, Guid establishmentId, int? rating, ReviewStatus? status, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken cancellationToken);
    Task<ReviewOrderProgress> GetProgressAsync(ReviewOrderContext order, CancellationToken cancellationToken);
}

public sealed record ReviewOrderContext(Guid TenantId, Guid EstablishmentId, Guid OrderId, string PublicReference,
    string Status, DateTimeOffset? CompletedAt, IReadOnlyList<ReviewOrderProduct> Products,
    string EstablishmentName = "", string? EstablishmentLogoUrl = null, string EstablishmentSlug = "");
public sealed record ReviewOrderProduct(Guid ProductId, string Name);
public sealed record ReviewProgress(Guid ProductId, string ProductName, bool Reviewed);
public sealed record ReviewOrderProgress(bool EstablishmentReviewed, bool PendingApproval, IReadOnlyList<ReviewProgress> Products);
public sealed record ReviewPublicItem(int Rating, string? Comment, DateTimeOffset CreatedAt);
public sealed record ReviewPublicPage(long Total, IReadOnlyList<ReviewPublicItem> Items);
public sealed record ReviewRatingSummary(Guid TargetId, decimal Average, long Count, long Rating1Count, long Rating2Count,
    long Rating3Count, long Rating4Count, long Rating5Count);
public sealed record ReviewAdminItem(Guid Id, Guid? ProductId, string? ProductName, int Rating, string? Comment,
    ReviewStatus Status, string? ModerationReason, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ReviewAdminPage(long Total, IReadOnlyList<ReviewAdminItem> Items);
