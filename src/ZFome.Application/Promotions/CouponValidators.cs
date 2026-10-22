using FluentValidation;

namespace ZFome.Application.Promotions;

public sealed class UpsertCouponCommandValidator : AbstractValidator<UpsertCouponCommand>
{
    public UpsertCouponCommandValidator()
    { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.Code).NotEmpty().MaximumLength(40); RuleFor(x => x.DiscountType).IsInEnum(); RuleFor(x => x.Value).GreaterThan(0); RuleFor(x => x.MinimumOrder).GreaterThanOrEqualTo(0); RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt); RuleFor(x => x.MaximumUses).GreaterThan(0).When(x => x.MaximumUses.HasValue); }
}
public sealed class SetCouponActiveCommandValidator : AbstractValidator<SetCouponActiveCommand>
{ public SetCouponActiveCommandValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.CouponId).NotEmpty(); } }
public sealed class ApplyCouponCommandValidator : AbstractValidator<ApplyCouponCommand>
{ public ApplyCouponCommandValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.OrderId).NotEmpty(); RuleFor(x => x.Code).NotEmpty().MaximumLength(40); } }
public sealed class RemoveCouponCommandValidator : AbstractValidator<RemoveCouponCommand>
{ public RemoveCouponCommandValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.OrderId).NotEmpty(); } }
public sealed class ListCouponsQueryValidator : AbstractValidator<ListCouponsQuery>
{ public ListCouponsQueryValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); } }
public sealed class SearchCouponsQueryValidator:AbstractValidator<SearchCouponsQuery>
{public SearchCouponsQueryValidator(){RuleFor(x=>x.EstablishmentId).NotEmpty();RuleFor(x=>x.Search).MaximumLength(100);RuleFor(x=>x.Page).GreaterThan(0);RuleFor(x=>x.PageSize).InclusiveBetween(1,100);}}
