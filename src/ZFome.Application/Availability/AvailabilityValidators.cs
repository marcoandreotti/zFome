using FluentValidation;

namespace ZFome.Application.Availability;

public sealed class ScheduleExceptionInputValidator : AbstractValidator<ScheduleExceptionInput>
{
    public ScheduleExceptionInputValidator()
    {
        RuleFor(x => x.ServiceType).IsInEnum().When(x => x.ServiceType is not null);
        RuleFor(x => x.OpensAt).NotNull().When(x => x.IsOpen);
        RuleFor(x => x.ClosesAt).NotNull().When(x => x.IsOpen);
        RuleFor(x => x).Must(x => !x.IsOpen || x.OpensAt != x.ClosesAt).WithMessage("Informe um intervalo válido para a exceção de horário aberto.");
        RuleFor(x => x).Must(x => x.IsOpen || x.OpensAt is null && x.ClosesAt is null).WithMessage("Exceções de horário fechado não podem conter um intervalo.");
        RuleFor(x => x.Reason).MaximumLength(250);
    }
}

public sealed class ReplaceScheduleExceptionsValidator : AbstractValidator<ReplaceScheduleExceptionsCommand>
{
    public ReplaceScheduleExceptionsValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty();
        RuleFor(x => x.Exceptions).NotNull().Must(x => x is null || x.Count <= 366);
        RuleForEach(x => x.Exceptions).NotNull().SetValidator(new ScheduleExceptionInputValidator());
    }
}

public sealed class PauseServiceValidator : AbstractValidator<PauseServiceCommand>
{
    public PauseServiceValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty();
        RuleFor(x => x.ServiceType).IsInEnum();
        RuleFor(x => x.EndsAt).GreaterThan(DateTimeOffset.UtcNow).When(x => x.EndsAt is not null);
        RuleFor(x => x.Reason).MaximumLength(250);
    }
}

public sealed class ResumeServiceValidator : AbstractValidator<ResumeServiceCommand>
{
    public ResumeServiceValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.ServiceType).IsInEnum(); }
}

public sealed class SetOfferUnavailabilityValidator : AbstractValidator<SetOfferUnavailabilityCommand>
{
    public SetOfferUnavailabilityValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.OfferId).NotEmpty();
        RuleFor(x => x.EndsAt).GreaterThan(DateTimeOffset.UtcNow).When(x => x.EndsAt is not null);
        RuleFor(x => x.Reason).MaximumLength(250);
    }
}

public sealed class ReactivateOfferValidator : AbstractValidator<ReactivateOfferCommand>
{
    public ReactivateOfferValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.Kind).IsInEnum(); RuleFor(x => x.OfferId).NotEmpty(); }
}
