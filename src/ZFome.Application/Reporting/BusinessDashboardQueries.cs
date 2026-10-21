using FluentValidation;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Abstractions.Reporting;
using ZFome.Application.Tenancy;
using ZFome.Domain.Ordering;

namespace ZFome.Application.Reporting;

public sealed record GetBusinessDashboardQuery(
    Guid EstablishmentId,
    DateOnly From,
    DateOnly To,
    OrderServiceType? ServiceType = null,
    int ProductPage = 1,
    int ProductPageSize = 10) : IQuery<BusinessDashboardReadModel>;

public sealed class GetBusinessDashboardQueryValidator : AbstractValidator<GetBusinessDashboardQuery>
{
    public GetBusinessDashboardQueryValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty();
        RuleFor(x => x.From).Must(x => x != default).WithMessage("A data inicial é obrigatória.");
        RuleFor(x => x.To).Must(x => x != default).WithMessage("A data final é obrigatória.");
        RuleFor(x => x).Must(x => x.To >= x.From)
            .WithMessage("A data final deve ser igual ou posterior à data inicial.");
        RuleFor(x => x).Must(x => x.To >= x.From && x.To.DayNumber - x.From.DayNumber < 366)
            .WithMessage("O período deve conter no máximo 366 dias.");
        RuleFor(x => x.ServiceType).IsInEnum().When(x => x.ServiceType.HasValue);
        RuleFor(x => x.ProductPage).InclusiveBetween(1, 10_000);
        RuleFor(x => x.ProductPageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetBusinessDashboardQueryHandler(
    EstablishmentScopeResolver scopeResolver,
    IBusinessDashboardReadGateway gateway)
    : IQueryHandler<GetBusinessDashboardQuery, BusinessDashboardReadModel>
{
    public async Task<BusinessDashboardReadModel> HandleAsync(
        GetBusinessDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(query.EstablishmentId, cancellationToken);
        return await gateway.GetAsync(
            scope.TenantId,
            scope.EstablishmentId,
            query.From,
            query.To,
            query.ServiceType?.ToString(),
            query.ProductPage,
            query.ProductPageSize,
            cancellationToken);
    }
}
