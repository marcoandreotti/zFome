using ZFome.Domain.Promotions;

namespace ZFome.Application.Abstractions.Promotions;

public interface ICouponRepository
{
    Task<Coupon?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken);
    Task<Coupon?> FindByCodeAsync(Guid tenantId, Guid establishmentId, string normalizedCode, CancellationToken cancellationToken);
    Task AddAsync(Coupon coupon, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ICouponReadGateway
{
    Task<IReadOnlyList<CouponReadModel>> ListAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken);
    Task<CouponSearchResult> SearchAsync(Guid tenantId, Guid establishmentId, string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken);
}

public sealed record CouponReadModel(Guid Id, string Code, string? Description, CouponDiscountType DiscountType, decimal Value, decimal MinimumOrder, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int? MaximumUses, int UsedCount, bool IsActive);
public sealed record CouponSearchResult(int Total, IReadOnlyList<CouponReadModel> Items);
