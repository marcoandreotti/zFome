using Microsoft.EntityFrameworkCore;
using ZFome.Application.Abstractions.Reviews;
using ZFome.Domain.Reviews;

namespace ZFome.Infrastructure.Persistence.Write.Repositories;

public sealed class ReviewRepository(ZFomeDbContext context) : IReviewWriteRepository
{
    public Task<ReviewRequest?> FindRequestAsync(Guid tenantId, Guid establishmentId, string key, CancellationToken cancellationToken) =>
        context.ReviewRequests.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Key == key, cancellationToken);

    public async Task<IReadOnlyCollection<Review>> FindForOrderAsync(Guid tenantId, Guid establishmentId, Guid orderId, CancellationToken cancellationToken) =>
        await context.Reviews.Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.OrderId == orderId).ToListAsync(cancellationToken);

    public Task<Review?> GetAsync(Guid tenantId, Guid establishmentId, Guid reviewId, CancellationToken cancellationToken) =>
        context.Reviews.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Id == reviewId, cancellationToken);

    public Task AddAsync(Review review, CancellationToken cancellationToken) => context.Reviews.AddAsync(review, cancellationToken).AsTask();
    public Task AddRequestAsync(ReviewRequest request, CancellationToken cancellationToken) => context.ReviewRequests.AddAsync(request, cancellationToken).AsTask();
    public Task AddModerationHistoryAsync(ReviewModerationHistory history, CancellationToken cancellationToken) => context.ReviewModerationHistories.AddAsync(history, cancellationToken).AsTask();

    public async Task ApplyRatingDeltaAsync(Guid tenantId, Guid establishmentId, ReviewTargetKind targetKind, Guid targetId, int rating, int delta, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _ = rating switch { 1 => true, 2 => true, 3 => true, 4 => true, 5 => true, _ => throw new ArgumentOutOfRangeException(nameof(rating)) };
        if (delta < 0)
        {
            var removed = await context.Database.ExecuteSqlInterpolatedAsync($"""
                update reviews.rating_summary
                set count = count - 1,
                    rating_sum = rating_sum - {rating},
                    rating_1_count = rating_1_count - {(rating == 1 ? 1 : 0)},
                    rating_2_count = rating_2_count - {(rating == 2 ? 1 : 0)},
                    rating_3_count = rating_3_count - {(rating == 3 ? 1 : 0)},
                    rating_4_count = rating_4_count - {(rating == 4 ? 1 : 0)},
                    rating_5_count = rating_5_count - {(rating == 5 ? 1 : 0)},
                    updated_at = {now}
                where tenant_id = {tenantId}
                  and establishment_id = {establishmentId}
                  and target_kind = {targetKind.ToString()}
                  and target_id = {targetId}
                  and count > 0
                """, cancellationToken);
            if (removed != 1) throw new InvalidOperationException("Rating summary counters would become negative.");
            return;
        }

        var affected = await context.Database.ExecuteSqlInterpolatedAsync($"""
            insert into reviews.rating_summary (tenant_id, establishment_id, target_kind, target_id, count, rating_sum,
                rating_1_count, rating_2_count, rating_3_count, rating_4_count, rating_5_count, updated_at)
            values ({tenantId}, {establishmentId}, {targetKind.ToString()}, {targetId}, {delta}, {rating * delta},
                {(rating == 1 ? delta : 0)}, {(rating == 2 ? delta : 0)}, {(rating == 3 ? delta : 0)}, {(rating == 4 ? delta : 0)}, {(rating == 5 ? delta : 0)}, {now})
            on conflict (tenant_id, establishment_id, target_kind, target_id) do update set
                count = reviews.rating_summary.count + excluded.count,
                rating_sum = reviews.rating_summary.rating_sum + excluded.rating_sum,
                rating_1_count = reviews.rating_summary.rating_1_count + excluded.rating_1_count,
                rating_2_count = reviews.rating_summary.rating_2_count + excluded.rating_2_count,
                rating_3_count = reviews.rating_summary.rating_3_count + excluded.rating_3_count,
                rating_4_count = reviews.rating_summary.rating_4_count + excluded.rating_4_count,
                rating_5_count = reviews.rating_summary.rating_5_count + excluded.rating_5_count,
                updated_at = excluded.updated_at
            where reviews.rating_summary.count + excluded.count >= 0
            """, cancellationToken);
        if (affected != 1) throw new InvalidOperationException("Rating summary counters would become negative.");
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}

public sealed class ReviewTransaction(ZFomeDbContext context) : IReviewTransaction
{
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
