using OrderHub.Application.Abstractions.Reviews;
using OrderHub.Application.Reviews;
using OrderHub.Domain.Reviews;

namespace OrderHub.Application.Tests.Reviews;

public sealed class PublishReviewsHandlerTests
{
    [Fact]
    public async Task Submission_persists_pending_reviews_without_public_summary_deltas()
    {
        var tenantId = Guid.NewGuid();
        var establishmentId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var order = new ReviewOrderContext(tenantId, establishmentId, Guid.NewGuid(), new string('a', 48),
            "Completed", DateTimeOffset.UtcNow.AddDays(-1), [new ReviewOrderProduct(productId, "Produto")]);
        var orders = new FakeOrders(order);
        var repository = new FakeReviewRepository();
        var handler = new PublishReviewsHandler(orders, repository, new InlineReviewTransaction(), TimeProvider.System);

        var result = await handler.HandleAsync(new PublishReviewsCommand(order.PublicReference, "request-key-1", 5, null,
            [new ReviewProductInput(productId, 4, null)]), CancellationToken.None);

        Assert.Equal(2, result.RatingsSubmitted);
        Assert.Equal(2, repository.Reviews.Count);
        Assert.All(repository.Reviews, review => Assert.Equal(ReviewStatus.Pending, review.Status));
        Assert.Empty(repository.SummaryDeltas);
        Assert.Single(repository.Requests);
    }

    [Fact]
    public async Task Approval_publishes_and_updates_the_rating_summary()
    {
        var tenantId = Guid.NewGuid();
        var establishmentId = Guid.NewGuid();
        var review = Review.Submit(tenantId, establishmentId, Guid.NewGuid(), ReviewTargetKind.Establishment,
            establishmentId, 5, null, DateTimeOffset.UtcNow);
        var repository = new FakeReviewRepository();
        repository.Reviews.Add(review);
        var handler = new ModerateReviewHandler(repository, new InlineReviewTransaction(), TimeProvider.System);

        await handler.HandleAsync(new ModerateReviewCommand(tenantId, establishmentId, Guid.NewGuid(), review.Id,
            null, ReviewModerationAction.Approved), CancellationToken.None);

        Assert.Equal(ReviewStatus.Published, review.Status);
        Assert.Equal([1], repository.SummaryDeltas);
        Assert.Equal(ReviewModerationAction.Approved, Assert.Single(repository.History).Action);
    }

    [Fact]
    public async Task Rejecting_pending_review_keeps_it_out_of_the_public_summary()
    {
        var tenantId = Guid.NewGuid();
        var establishmentId = Guid.NewGuid();
        var review = Review.Submit(tenantId, establishmentId, Guid.NewGuid(), ReviewTargetKind.Establishment,
            establishmentId, 5, null, DateTimeOffset.UtcNow);
        var repository = new FakeReviewRepository();
        repository.Reviews.Add(review);
        var handler = new ModerateReviewHandler(repository, new InlineReviewTransaction(), TimeProvider.System);

        await handler.HandleAsync(new ModerateReviewCommand(tenantId, establishmentId, Guid.NewGuid(), review.Id,
            "Contains personal information", ReviewModerationAction.Rejected), CancellationToken.None);

        Assert.Equal(ReviewStatus.Rejected, review.Status);
        Assert.Empty(repository.SummaryDeltas);
        Assert.Equal("Contains personal information", review.ModerationReason);
        Assert.Equal(ReviewModerationAction.Rejected, Assert.Single(repository.History).Action);
        await Assert.ThrowsAsync<OrderHub.Domain.Exceptions.DomainException>(() =>
            handler.HandleAsync(new ModerateReviewCommand(tenantId, establishmentId, Guid.NewGuid(), review.Id,
                null, ReviewModerationAction.Restored), CancellationToken.None));
    }

    private sealed class FakeOrders(ReviewOrderContext order) : IReviewOrderGateway
    {
        public Task<ReviewOrderContext?> FindAsync(string publicReference, CancellationToken cancellationToken) =>
            Task.FromResult<ReviewOrderContext?>(publicReference == order.PublicReference ? order : null);
    }

    private sealed class InlineReviewTransaction : IReviewTransaction
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);
    }

    private sealed class FakeReviewRepository : IReviewWriteRepository
    {
        public List<Review> Reviews { get; } = [];
        public List<ReviewRequest> Requests { get; } = [];
        public List<int> SummaryDeltas { get; } = [];
        public List<ReviewModerationHistory> History { get; } = [];
        public Task<ReviewRequest?> FindRequestAsync(Guid tenantId, Guid establishmentId, string key, CancellationToken cancellationToken) =>
            Task.FromResult(Requests.SingleOrDefault(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Key == key));
        public Task<IReadOnlyCollection<Review>> FindForOrderAsync(Guid tenantId, Guid establishmentId, Guid orderId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Review>>(Reviews.Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.OrderId == orderId).ToArray());
        public Task<Review?> GetAsync(Guid tenantId, Guid establishmentId, Guid reviewId, CancellationToken cancellationToken) =>
            Task.FromResult(Reviews.SingleOrDefault(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Id == reviewId));
        public Task AddAsync(Review review, CancellationToken cancellationToken) { Reviews.Add(review); return Task.CompletedTask; }
        public Task AddRequestAsync(ReviewRequest request, CancellationToken cancellationToken) { Requests.Add(request); return Task.CompletedTask; }
        public Task AddModerationHistoryAsync(ReviewModerationHistory history, CancellationToken cancellationToken) { History.Add(history); return Task.CompletedTask; }
        public Task ApplyRatingDeltaAsync(Guid tenantId, Guid establishmentId, ReviewTargetKind targetKind, Guid targetId, int rating, int delta, DateTimeOffset now, CancellationToken cancellationToken) { SummaryDeltas.Add(delta); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
