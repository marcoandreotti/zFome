using FluentValidation;

namespace ZFome.Application.Catalog;

public sealed class UpsertCategoryCommandValidator : AbstractValidator<UpsertCategoryCommand>
{
    public UpsertCategoryCommandValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.Name).NotEmpty().MaximumLength(150); RuleFor(x => x.Description).MaximumLength(500); RuleFor(x => x.Order).GreaterThanOrEqualTo(0); RuleFor(x => x.ImageUrl).MaximumLength(500).Must(x => x is null || Uri.TryCreate(x, UriKind.Absolute, out _)); }
}
public sealed class UpsertProductCommandValidator : AbstractValidator<UpsertProductCommand>
{
    public UpsertProductCommandValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.CategoryId).NotEmpty(); RuleFor(x => x.Code).NotEmpty().MaximumLength(50); RuleFor(x => x.Name).NotEmpty().MaximumLength(150); RuleFor(x => x.Description).MaximumLength(1000); RuleFor(x => x.BasePrice).GreaterThanOrEqualTo(0); RuleForEach(x => x.Images).ChildRules(x => { x.RuleFor(y => y.Url).NotEmpty().MaximumLength(500).Must(y => Uri.TryCreate(y, UriKind.Absolute, out _)); x.RuleFor(y => y.Order).GreaterThanOrEqualTo(0); }); RuleFor(x => x.Images.Count(y => y.IsPrincipal)).LessThanOrEqualTo(1); RuleForEach(x => x.Variations).ChildRules(x => { x.RuleFor(y => y.Name).NotEmpty().MaximumLength(100); x.RuleFor(y => y.Price).GreaterThanOrEqualTo(0); x.RuleFor(y => y.Order).GreaterThanOrEqualTo(0); }); RuleForEach(x => x.AdditionalGroups).ChildRules(x => { x.RuleFor(y => y.GroupId).NotEmpty(); x.RuleFor(y => y.Order).GreaterThanOrEqualTo(0); }); }
}
public sealed class UpsertAdditionalCommandValidator : AbstractValidator<UpsertAdditionalCommand>
{
    public UpsertAdditionalCommandValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.Name).NotEmpty().MaximumLength(150); RuleFor(x => x.Price).GreaterThanOrEqualTo(0); }
}
public sealed class UpsertAdditionalGroupCommandValidator : AbstractValidator<UpsertAdditionalGroupCommand>
{
    public UpsertAdditionalGroupCommandValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.Name).NotEmpty().MaximumLength(150); RuleFor(x => x.MinimumSelection).GreaterThanOrEqualTo(0); RuleFor(x => x.MaximumSelection).GreaterThan(0).GreaterThanOrEqualTo(x => x.MinimumSelection); RuleFor(x => x.PricingStrategy).IsInEnum(); RuleFor(x => x.Type).IsInEnum(); RuleFor(x => x.Items).Must(x => x.Select(y => y.AdditionalId).Distinct().Count() == x.Count); RuleFor(x => x.Items).Must(items => items.All(item => (item.CompatibilityRules ?? Array.Empty<AdditionalGroupCompatibilityInput>()).All(rule => rule.TargetGroupId != Guid.Empty && rule.TargetAdditionalId != Guid.Empty && Enum.IsDefined(rule.Kind)))); RuleForEach(x => x.Items).ChildRules(x => { x.RuleFor(y => y.AdditionalId).NotEmpty(); x.RuleFor(y => y.Order).GreaterThanOrEqualTo(0); }); }
}
public sealed class GetAdministrativeCatalogQueryValidator : AbstractValidator<GetAdministrativeCatalogQuery> { public GetAdministrativeCatalogQueryValidator() => RuleFor(x => x.EstablishmentId).NotEmpty(); }
public sealed class GetPublicCatalogQueryValidator : AbstractValidator<GetPublicCatalogQuery> { public GetPublicCatalogQueryValidator() => RuleFor(x => x.Slug).NotEmpty().MaximumLength(100); }
