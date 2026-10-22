using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Promotions;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Exceptions;
using ZFome.Application.Tenancy;
using ZFome.Domain.Promotions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Application.Promotions;

public sealed class UpsertCouponCommandHandler(EstablishmentScopeResolver scopeResolver, ICouponRepository repository, TimeProvider timeProvider) : ICommandHandler<UpsertCouponCommand, Guid>
{
    public async Task<Guid> HandleAsync(UpsertCouponCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken); var code = Coupon.NormalizeCode(command.Code);
        var conflicting = await repository.FindByCodeAsync(scope.TenantId, scope.EstablishmentId, code, cancellationToken);
        if (conflicting is not null && conflicting.Id != command.Id) throw new ConflictException("Coupon code is already in use in this establishment.");
        Coupon coupon; var now = timeProvider.GetUtcNow();
        if (command.Id is { } id)
        {
            coupon = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, id, cancellationToken) ?? throw new NotFoundException("Coupon was not found.");
            coupon.Update(command.Code, command.Description, command.DiscountType, command.Value, new Money(command.MinimumOrder), command.StartsAt, command.EndsAt, command.MaximumUses, now);
            await repository.SaveChangesAsync(cancellationToken);
        }
        else
        {
            coupon = Coupon.Create(scope.TenantId, scope.EstablishmentId, command.Code, command.Description, command.DiscountType, command.Value, new Money(command.MinimumOrder), command.StartsAt, command.EndsAt, command.MaximumUses, now);
            await repository.AddAsync(coupon, cancellationToken);
        }
        return coupon.Id;
    }
}

public sealed class SetCouponActiveCommandHandler(EstablishmentScopeResolver scopeResolver, ICouponRepository repository, TimeProvider timeProvider) : ICommandHandler<SetCouponActiveCommand>
{
    public async Task HandleAsync(SetCouponActiveCommand command, CancellationToken cancellationToken)
    { var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken); var coupon = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, command.CouponId, cancellationToken) ?? throw new NotFoundException("Coupon was not found."); if (command.IsActive) coupon.Activate(timeProvider.GetUtcNow()); else coupon.Deactivate(timeProvider.GetUtcNow()); await repository.SaveChangesAsync(cancellationToken); }
}

public sealed class ApplyCouponCommandHandler(EstablishmentScopeResolver scopeResolver, IOrderRepository orders, ICouponRepository coupons, TimeProvider timeProvider) : ICommandHandler<ApplyCouponCommand>
{
    public async Task HandleAsync(ApplyCouponCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken);
        var order = await orders.GetAsync(scope.TenantId, scope.EstablishmentId, command.OrderId, cancellationToken) ?? throw new NotFoundException("Order was not found.");
        var coupon = await coupons.FindByCodeAsync(scope.TenantId, scope.EstablishmentId, Coupon.NormalizeCode(command.Code), cancellationToken) ?? throw new NotFoundException("Coupon was not found.");
        var evaluation = coupon.Evaluate(order.Subtotal, timeProvider.GetUtcNow()); order.ApplyCoupon(evaluation.CouponId, evaluation.Code, evaluation.Discount, timeProvider.GetUtcNow()); await orders.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RemoveCouponCommandHandler(EstablishmentScopeResolver scopeResolver, IOrderRepository orders, TimeProvider timeProvider) : ICommandHandler<RemoveCouponCommand>
{
    public async Task HandleAsync(RemoveCouponCommand command, CancellationToken cancellationToken)
    { var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken); var order = await orders.GetAsync(scope.TenantId, scope.EstablishmentId, command.OrderId, cancellationToken) ?? throw new NotFoundException("Order was not found."); order.RemoveCoupon(timeProvider.GetUtcNow()); await orders.SaveChangesAsync(cancellationToken); }
}

public sealed class ListCouponsQueryHandler(EstablishmentScopeResolver scopeResolver, ICouponReadGateway gateway) : IQueryHandler<ListCouponsQuery, IReadOnlyList<CouponReadModel>>
{
    public async Task<IReadOnlyList<CouponReadModel>> HandleAsync(ListCouponsQuery query, CancellationToken cancellationToken)
    { var scope = await scopeResolver.ResolveAsync(query.EstablishmentId, cancellationToken); return await gateway.ListAsync(scope.TenantId, scope.EstablishmentId, cancellationToken); }
}
public sealed class SearchCouponsQueryHandler(EstablishmentScopeResolver scopeResolver,ICouponReadGateway gateway):IQueryHandler<SearchCouponsQuery,CouponSearchResult>
{ public async Task<CouponSearchResult> HandleAsync(SearchCouponsQuery query,CancellationToken cancellationToken){var scope=await scopeResolver.ResolveAsync(query.EstablishmentId,cancellationToken);return await gateway.SearchAsync(scope.TenantId,scope.EstablishmentId,query.Search,query.IsActive,query.Page,query.PageSize,cancellationToken);} }
