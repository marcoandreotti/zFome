using FluentValidation;

namespace ZFome.Application.Onboarding;

public sealed class GetOnboardingValidator : AbstractValidator<GetOnboardingQuery>
{
    public GetOnboardingValidator() => RuleFor(x => x.EstablishmentId).NotEmpty();
}
public sealed class GetConfigurationValidator : AbstractValidator<GetConfigurationQuery>
{
    public GetConfigurationValidator() => RuleFor(x => x.EstablishmentId).NotEmpty();
}
public sealed class SearchTablesValidator : AbstractValidator<SearchTablesQuery>
{
    public SearchTablesValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.Page).InclusiveBetween(1, 1000000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100); }
}
public sealed class UpdateEstablishmentValidator : AbstractValidator<UpdateEstablishmentCommand>
{
    public UpdateEstablishmentValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.TradeName).NotEmpty().MaximumLength(150); RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches("^[a-zA-Z0-9]+(?:-[a-zA-Z0-9]+)*$"); RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(100); }
}
public sealed class ThemeInputValidator : AbstractValidator<ThemeInput>
{
    public ThemeInputValidator()
    {
        RuleFor(x => x.PrimaryColor).Matches("^#[a-fA-F0-9]{6}$"); RuleFor(x => x.SecondaryColor).Matches("^#[a-fA-F0-9]{6}$");
        RuleFor(x => x.BackgroundColor).Matches("^#[a-fA-F0-9]{6}$"); RuleFor(x => x.TextColor).Matches("^#[a-fA-F0-9]{6}$");
        RuleFor(x => x.FontFamily).MaximumLength(100);
        RuleFor(x => x.LogoUrl).MaximumLength(500).Must(SafeUrl); RuleFor(x => x.FaviconUrl).MaximumLength(500).Must(SafeUrl);
    }
    private static bool SafeUrl(string? value) => string.IsNullOrWhiteSpace(value) || Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
public sealed class UpdateThemeValidator : AbstractValidator<UpdateThemeCommand>
{
    public UpdateThemeValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.Theme).NotNull().SetValidator(new ThemeInputValidator()); }
}
public sealed class HoursInputValidator : AbstractValidator<HoursInput>
{
    public HoursInputValidator() { RuleFor(x => x.DayOfWeek).IsInEnum(); }
}
public sealed class ReplaceBusinessHoursValidator : AbstractValidator<ReplaceBusinessHoursCommand>
{
    public ReplaceBusinessHoursValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.Hours).NotNull().Must(x => x is null || x.Count <= 100); RuleForEach(x => x.Hours).NotNull().SetValidator(new HoursInputValidator()); }
}
public sealed class CreateTableValidator : AbstractValidator<CreateTableCommand>
{
    public CreateTableValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.IntentId).NotEmpty(); RuleFor(x => x.Code).NotEmpty().MaximumLength(30); RuleFor(x => x.Description).MaximumLength(100); }
}
public sealed class UpdateTableValidator : AbstractValidator<UpdateTableCommand>
{
    public UpdateTableValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.TableId).NotEmpty(); RuleFor(x => x.Code).NotEmpty().MaximumLength(30); RuleFor(x => x.Description).MaximumLength(100); }
}
public sealed class RotateTableTokenValidator : AbstractValidator<RotateTableTokenCommand>
{
    public RotateTableTokenValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.TableId).NotEmpty(); }
}
public sealed class CompleteOnboardingValidator : AbstractValidator<CompleteOnboardingCommand>
{
    public CompleteOnboardingValidator() => RuleFor(x => x.EstablishmentId).NotEmpty();
}
