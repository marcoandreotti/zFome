using FluentValidation;
using ZFome.Application.Identity;

namespace ZFome.Application.Identity.CreateAdministrativeUser;

public sealed class CreateAdministrativeUserCommandValidator : AbstractValidator<CreateAdministrativeUserCommand>
{
    public CreateAdministrativeUserCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(command => command.Password).NotEmpty().MinimumLength(PasswordPolicy.MinimumLength).MaximumLength(PasswordPolicy.MaximumLength);
        RuleFor(command => command.InitialRole).IsInEnum();
        RuleFor(command => command.EstablishmentId).NotEmpty();
    }
}
