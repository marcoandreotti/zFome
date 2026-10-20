using Dapper;
using Microsoft.EntityFrameworkCore;
using OrderHub.Application.Abstractions.Persistence;
using OrderHub.Application.Abstractions.Reviews;
using OrderHub.Domain.Reviews;
using OrderHub.Infrastructure.Persistence.Write;

namespace OrderHub.Infrastructure.Persistence.Read;

public sealed class ReviewOrderGateway(OrderHubDbContext context) : IReviewOrderGateway
{
    public async Task<ReviewOrderContext?> FindAsync(string publicReference, CancellationToken cancellationToken)
    {
        var order = await context.Orders.AsSplitQuery()
            .Include(x => x.Items)
            .Include(x => x.History)
            .SingleOrDefaultAsync(x => x.PublicReference == publicReference, cancellationToken);
        if (order is null) return null;
        var products = order.Items.GroupBy(x => x.ProductId).Select(group =>
            new ReviewOrderProduct(group.Key, group.First().ProductName)).ToArray();
        var completedAt = order.History.Where(x => x.NewStatus == OrderHub.Domain.Ordering.OrderStatus.Completed)
            .Select(x => (DateTimeOffset?)x.OccurredAt).Max();
        var establishment = await context.Establishments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == order.TenantId && x.Id == order.EstablishmentId, cancellationToken);
        return new(order.TenantId, order.EstablishmentId, order.Id,
            order.PublicReference ?? throw new InvalidOperationException("Review order must have a public reference."),
            order.Status.ToString(), completedAt, products, establishment?.TradeName ?? "Estabelecimento",
            establishment?.Theme.LogoUrl, establishment?.Slug.Value ?? "");
    }
}

public sealed class ReviewReadGateway(IReadConnectionFactory connections) : IReviewReadGateway
{
    public async Task<ReviewPublicPage> ListPublicAsync(Guid tenantId, Guid establishmentId, Guid? productId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var db = await connections.OpenConnectionAsync(cancellationToken);
        var filter = "tenant_id=@tenantId and establishment_id=@establishmentId and status='Published' and target_kind=@kind and target_id=@targetId";
        var parameters = new { tenantId, establishmentId, kind = productId.HasValue ? "Product" : "Establishment", targetId = productId ?? establishmentId, offset = (page - 1) * pageSize, pageSize };
        var total = await db.ExecuteScalarAsync<long>(new CommandDefinition($"select count(*) from reviews.review where {filter}", parameters, cancellationToken: cancellationToken));
        var items = (await db.QueryAsync<ReviewPublicItem>(new CommandDefinition($"select rating Rating, comment Comment, created_at CreatedAt from reviews.review where {filter} order by created_at desc, id desc offset @offset limit @pageSize", parameters, cancellationToken: cancellationToken))).ToArray();
        return new(total, items);
    }

    public async Task<ReviewRatingSummary?> GetSummaryAsync(Guid tenantId, Guid establishmentId, ReviewTargetKind targetKind, Guid targetId, CancellationToken cancellationToken)
    {
        await using var db = await connections.OpenConnectionAsync(cancellationToken);
        return await db.QuerySingleOrDefaultAsync<ReviewRatingSummary>(new CommandDefinition("""
            select target_id TargetId, (rating_sum::numeric / nullif(count,0))::numeric(4,2) Average, count Count,
                rating_1_count Rating1Count, rating_2_count Rating2Count, rating_3_count Rating3Count,
                rating_4_count Rating4Count, rating_5_count Rating5Count
            from reviews.rating_summary where tenant_id=@tenantId and establishment_id=@establishmentId and target_kind=@targetKind and target_id=@targetId and count>0
            """, new { tenantId, establishmentId, targetKind = targetKind.ToString(), targetId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ReviewRatingSummary>> GetProductSummariesAsync(Guid tenantId, Guid establishmentId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0) return [];
        await using var db = await connections.OpenConnectionAsync(cancellationToken);
        return (await db.QueryAsync<ReviewRatingSummary>(new CommandDefinition("""
            select target_id TargetId, (rating_sum::numeric / nullif(count,0))::numeric(4,2) Average, count Count,
                rating_1_count Rating1Count, rating_2_count Rating2Count, rating_3_count Rating3Count,
                rating_4_count Rating4Count, rating_5_count Rating5Count
            from reviews.rating_summary where tenant_id=@tenantId and establishment_id=@establishmentId and target_kind='Product' and target_id=any(@productIds) and count>0
            """, new { tenantId, establishmentId, productIds = productIds.ToArray() }, cancellationToken: cancellationToken))).ToArray();
    }

    public async Task<ReviewAdminPage> ListAdminAsync(Guid tenantId, Guid establishmentId, int? rating, ReviewStatus? status, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var db = await connections.OpenConnectionAsync(cancellationToken);
        const string filter = "r.tenant_id=@tenantId and r.establishment_id=@establishmentId and (@rating is null or r.rating=@rating) and (@status is null or r.status=@status) and (@from is null or r.created_at>=@from) and (@to is null or r.created_at<@to)";
        var p = new { tenantId, establishmentId, rating, status = status?.ToString(), from, to, offset = (page - 1) * pageSize, pageSize };
        var total = await db.ExecuteScalarAsync<long>(new CommandDefinition($"select count(*) from reviews.review r where {filter}", p, cancellationToken: cancellationToken));
        var items = (await db.QueryAsync<ReviewAdminItem>(new CommandDefinition($"""
            select r.id Id, case when r.target_kind='Product' then r.target_id else null end ProductId, p.name ProductName,
                r.rating Rating, r.comment Comment, r.status Status, r.moderation_reason ModerationReason, r.created_at CreatedAt, r.updated_at UpdatedAt
            from reviews.review r left join catalog.product p on p.tenant_id=r.tenant_id and p.establishment_id=r.establishment_id and p.id=r.target_id and r.target_kind='Product'
            where {filter} order by r.created_at desc,r.id desc offset @offset limit @pageSize
            """, p, cancellationToken: cancellationToken))).ToArray();
        return new(total, items);
    }

    public async Task<ReviewOrderProgress> GetProgressAsync(ReviewOrderContext order, CancellationToken cancellationToken)
    {
        await using var db = await connections.OpenConnectionAsync(cancellationToken);
        var targets = (await db.QueryAsync<ReviewedTarget>(new CommandDefinition("select target_kind TargetKind, target_id TargetId, status Status from reviews.review where tenant_id=@TenantId and establishment_id=@EstablishmentId and order_id=@OrderId", order, cancellationToken: cancellationToken))).ToArray();
        var establishmentReviewed = targets.Any(x => x.TargetKind == "Establishment" && x.TargetId == order.EstablishmentId);
        var productIds = targets.Where(x => x.TargetKind == "Product").Select(x => x.TargetId).ToHashSet();
        var pending = targets.Any(x => x.Status == "Pending");
        return new(establishmentReviewed, pending, order.Products.Select(x => new ReviewProgress(x.ProductId, x.Name, productIds.Contains(x.ProductId))).ToArray());
    }

    private sealed record ReviewedTarget(string TargetKind, Guid TargetId, string Status);
}
