using FluentValidation;
using ZFome.Domain.Ordering;

namespace ZFome.Application.PublicOrdering;

public sealed class GetPublicContextQueryValidator : AbstractValidator<GetPublicContextQuery>
{ public GetPublicContextQueryValidator() { RuleFor(x => x.Slug).NotEmpty().MaximumLength(100); RuleFor(x => x.TableToken).MaximumLength(200); } }

public sealed class UpsertPublicCustomerCommandValidator : AbstractValidator<UpsertPublicCustomerCommand>
{ public UpsertPublicCustomerCommandValidator() { RuleFor(x => x.Slug).NotEmpty().MaximumLength(100); RuleFor(x => x.Name).NotEmpty().MaximumLength(150); RuleFor(x => x.Phone).NotEmpty().MaximumLength(30); RuleFor(x => x.Email).EmailAddress().MaximumLength(254).When(x => x.Email is not null); } }

public sealed class SimulatePublicOrderQueryValidator : AbstractValidator<SimulatePublicOrderQuery>
{
    public SimulatePublicOrderQueryValidator()
    {
        RuleFor(x=>x.Slug).NotEmpty().MaximumLength(100); RuleFor(x=>x.ServiceType).IsInEnum();
        RuleFor(x=>x.TableToken).NotEmpty().When(x=>x.ServiceType==OrderServiceType.Table);
        RuleFor(x=>x.DeliveryAddress).NotNull().When(x=>x.ServiceType==OrderServiceType.Delivery);
        RuleFor(x => x.ScheduledAtUtc).Null().When(x => x.ServiceType == OrderServiceType.Table);
        RuleFor(x=>x.Items).NotEmpty().Must(x=>x.Count<=100); RuleForEach(x=>x.Items).SetValidator(new PublicOrderLineValidator());
    }
}

public sealed class ConfirmPublicOrderCommandValidator : AbstractValidator<ConfirmPublicOrderCommand>
{
    public ConfirmPublicOrderCommandValidator()
    {
        RuleFor(x=>x.Slug).NotEmpty().MaximumLength(100); RuleFor(x=>x.ServiceType).IsInEnum();
        RuleFor(x=>x.TableToken).NotEmpty().When(x=>x.ServiceType==OrderServiceType.Table);
        RuleFor(x=>x.DeliveryAddress).NotNull().When(x=>x.ServiceType==OrderServiceType.Delivery);
        RuleFor(x => x.ScheduledAtUtc).Null().When(x => x.ServiceType == OrderServiceType.Table);
        RuleFor(x=>x.Items).NotEmpty().Must(x=>x.Count<=100); RuleForEach(x=>x.Items).SetValidator(new PublicOrderLineValidator());
        RuleFor(x => x.IdempotencyKey).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleFor(x => x.PaymentMethodId).NotEmpty();
        RuleFor(x => x.ReceivedAmount).GreaterThan(0).When(x => x.ReceivedAmount is not null);
        RuleFor(x => x.DeliveryRegionId).NotEmpty().When(x => x.ServiceType == OrderServiceType.Delivery);
        RuleFor(x => x.ExpectedDeliveryFee).NotNull().GreaterThanOrEqualTo(0).When(x => x.ServiceType == OrderServiceType.Delivery);
        RuleFor(x => x.ExpectedDeliveryEstimatedMinutes).NotNull().InclusiveBetween(1, 1440).When(x => x.ServiceType == OrderServiceType.Delivery);
        RuleFor(x => x.DeliveryQuoteIssuedAt).NotNull().When(x => x.ServiceType == OrderServiceType.Delivery);
    }
}

public sealed class GetPublicOrderQueryValidator : AbstractValidator<GetPublicOrderQuery>
{ public GetPublicOrderQueryValidator() { RuleFor(x => x.Reference).NotEmpty().Length(48).Matches("^[a-f0-9]+$"); } }

public sealed class CancelPublicOrderCommandValidator : AbstractValidator<CancelPublicOrderCommand>
{ public CancelPublicOrderCommandValidator() { RuleFor(x => x.Reference).NotEmpty().Length(48).Matches("^[a-f0-9]+$"); RuleFor(x => x.Reason).MaximumLength(500); } }

internal sealed class PublicOrderLineValidator : AbstractValidator<Abstractions.PublicOrdering.PublicOrderLine>
{
    public PublicOrderLineValidator()
    {
        RuleFor(x=>x.ProductId).NotEmpty(); RuleFor(x=>x.Quantity).GreaterThan(0); RuleFor(x=>x.Notes).MaximumLength(500);
        RuleForEach(x=>x.Additionals).ChildRules(a=>{a.RuleFor(x=>x.AdditionalId).NotEmpty();a.RuleFor(x=>x.Quantity).GreaterThan(0);});
    }
}
