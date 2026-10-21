using ZFome.Application.Abstractions.Commands;
using ZFome.Domain.Promotions;

namespace ZFome.Application.Promotions;

public sealed record UpsertCouponCommand(Guid EstablishmentId, Guid? Id, string Code, string? Description, CouponDiscountType DiscountType, decimal Value, decimal MinimumOrder, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int? MaximumUses) : ICommand<Guid>;
public sealed record SetCouponActiveCommand(Guid EstablishmentId, Guid CouponId, bool IsActive) : ICommand;
public sealed record ApplyCouponCommand(Guid EstablishmentId, Guid OrderId, string Code) : ICommand;
public sealed record RemoveCouponCommand(Guid EstablishmentId, Guid OrderId) : ICommand;
