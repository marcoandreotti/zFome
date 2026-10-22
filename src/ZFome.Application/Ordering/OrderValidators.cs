using FluentValidation;
using ZFome.Domain.Ordering;

namespace ZFome.Application.Ordering;

public sealed class CreateOrderDraftCommandValidator : AbstractValidator<CreateOrderDraftCommand>
{
    public CreateOrderDraftCommandValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty();
        RuleFor(x => x.ServiceType).IsInEnum();
        RuleFor(x => x.TableId).NotNull().When(x => x.ServiceType == OrderServiceType.Table);
        RuleFor(x => x.TableId).Null().When(x => x.ServiceType != OrderServiceType.Table);
        RuleFor(x => x.DeliveryAddress).NotNull().When(x => x.ServiceType == OrderServiceType.Delivery && x.CustomerAddressId is null);
    }
}

public sealed class AddOrderItemCommandValidator : AbstractValidator<AddOrderItemCommand>
{
    public AddOrderItemCommandValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.OrderId).NotEmpty(); RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0); RuleFor(x => x.Notes).MaximumLength(500);
        RuleForEach(x => x.Additionals).ChildRules(item => { item.RuleFor(x => x.AdditionalId).NotEmpty(); item.RuleFor(x => x.Quantity).GreaterThan(0); });
    }
}

public sealed class ConfirmOrderCommandValidator : AbstractValidator<ConfirmOrderCommand>
{ public ConfirmOrderCommandValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.OrderId).NotEmpty(); } }

public sealed class TransitionOrderCommandValidator : AbstractValidator<TransitionOrderCommand>
{ public TransitionOrderCommandValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.OrderId).NotEmpty(); RuleFor(x => x.NewStatus).IsInEnum(); RuleFor(x => x.Note).MaximumLength(500); } }

public sealed class GetOrderQueryValidator : AbstractValidator<GetOrderQuery>
{ public GetOrderQueryValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.OrderId).NotEmpty(); } }

public sealed class SearchOrdersQueryValidator : AbstractValidator<SearchOrdersQuery>
{ public SearchOrdersQueryValidator() { RuleFor(x=>x.EstablishmentId).NotEmpty(); RuleFor(x=>x.Page).GreaterThan(0); RuleFor(x=>x.PageSize).InclusiveBetween(1,100); RuleFor(x=>x.To).GreaterThanOrEqualTo(x=>x.From).When(x=>x.From.HasValue&&x.To.HasValue); RuleFor(x=>x.Number).GreaterThan(0).When(x=>x.Number.HasValue); RuleFor(x=>x.Status).IsInEnum().When(x=>x.Status.HasValue); RuleFor(x=>x.ServiceType).IsInEnum().When(x=>x.ServiceType.HasValue); } }
