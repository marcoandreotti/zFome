using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Queries;
using ZFome.Domain.Ordering;

namespace ZFome.Application.Ordering;

public sealed record GetOrderQuery(Guid EstablishmentId, Guid OrderId) : IQuery<OrderReadModel>;
public sealed record SearchOrdersQuery(Guid EstablishmentId, DateTimeOffset? From, DateTimeOffset? To, OrderStatus? Status, long? Number, OrderServiceType? ServiceType, int Page = 1, int PageSize = 20) : IQuery<OrderSearchResult>;
