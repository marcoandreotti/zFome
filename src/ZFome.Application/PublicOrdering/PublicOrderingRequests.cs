using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.PublicOrdering;
using ZFome.Application.Abstractions.Queries;
using ZFome.Domain.Ordering;

namespace ZFome.Application.PublicOrdering;

public sealed record GetPublicContextQuery(string Slug, string? TableToken) : IQuery<PublicOrderingContext>;
public sealed record UpsertPublicCustomerCommand(string Slug, string Name, string Phone, string? Email, PublicAddress? Address) : ICommand<PublicCustomerResult>;
public sealed record PublicCustomerResult(Guid CustomerId, Guid? AddressId);
public sealed record SimulatePublicOrderQuery(string Slug, OrderServiceType ServiceType, Guid? CustomerId, Guid? CustomerAddressId, string? TableToken, PublicAddress? DeliveryAddress, string? CouponCode, Guid? PaymentMethodId, IReadOnlyCollection<PublicOrderLine> Items, DateTimeOffset? ScheduledAtUtc = null) : IQuery<PublicSimulation>;
public sealed record ConfirmPublicOrderCommand(string Slug, string IdempotencyKey, OrderServiceType ServiceType, Guid? CustomerId, Guid? CustomerAddressId, string? TableToken, PublicAddress? DeliveryAddress, string? CouponCode, Guid PaymentMethodId, decimal? ReceivedAmount, IReadOnlyCollection<PublicOrderLine> Items,
    Guid? DeliveryRegionId = null, decimal? ExpectedDeliveryFee = null, int? ExpectedDeliveryEstimatedMinutes = null, DateTimeOffset? DeliveryQuoteIssuedAt = null, DateTimeOffset? ScheduledAtUtc = null) : ICommand<PublicConfirmation>;
public sealed record GetPublicOrderQuery(string Reference) : IQuery<OrderReadModel>;
public sealed record CancelPublicOrderCommand(string Reference, string? Reason) : ICommand;
