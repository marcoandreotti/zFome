using ZFome.Application.Abstractions.Promotions;
using ZFome.Application.Abstractions.Queries;

namespace ZFome.Application.Promotions;

public sealed record ListCouponsQuery(Guid EstablishmentId) : IQuery<IReadOnlyList<CouponReadModel>>;
public sealed record SearchCouponsQuery(Guid EstablishmentId,string? Search,bool? IsActive,int Page=1,int PageSize=20):IQuery<CouponSearchResult>;
