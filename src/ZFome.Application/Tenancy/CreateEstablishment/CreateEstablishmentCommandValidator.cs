using FluentValidation;

namespace ZFome.Application.Tenancy.CreateEstablishment;

public sealed class CreateEstablishmentCommandValidator : AbstractValidator<CreateEstablishmentCommand>
{
    public CreateEstablishmentCommandValidator()
    {
        RuleFor(command => command.TradeName).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Slug).NotEmpty().MaximumLength(100);
    }
}
