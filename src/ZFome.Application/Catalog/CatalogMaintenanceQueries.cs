using FluentValidation;
using ZFome.Application.Abstractions.Catalog;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Tenancy;

namespace ZFome.Application.Catalog;

public sealed record SearchAdditionalsQuery(Guid EstablishmentId, string? Search = null, bool? IsActive = null, int Page = 1, int PageSize = 20) : IQuery<AdditionalSearchResult>;
public sealed record SearchAdditionalGroupsQuery(Guid EstablishmentId, string? Search = null, bool? IsActive = null, int Page = 1, int PageSize = 20) : IQuery<AdditionalGroupSearchResult>;

public sealed class SearchAdditionalsQueryHandler(EstablishmentScopeResolver scopeResolver, ICatalogMaintenanceReadGateway gateway) : IQueryHandler<SearchAdditionalsQuery, AdditionalSearchResult>
{
    public async Task<AdditionalSearchResult> HandleAsync(SearchAdditionalsQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(query.EstablishmentId, cancellationToken);
        return await gateway.SearchAdditionalsAsync(scope.TenantId, query, cancellationToken);
    }
}

public sealed class SearchAdditionalGroupsQueryHandler(EstablishmentScopeResolver scopeResolver, ICatalogMaintenanceReadGateway gateway) : IQueryHandler<SearchAdditionalGroupsQuery, AdditionalGroupSearchResult>
{
    public async Task<AdditionalGroupSearchResult> HandleAsync(SearchAdditionalGroupsQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(query.EstablishmentId, cancellationToken);
        return await gateway.SearchGroupsAsync(scope.TenantId, query, cancellationToken);
    }
}

public sealed class SearchAdditionalsQueryValidator : AbstractValidator<SearchAdditionalsQuery>
{
    public SearchAdditionalsQueryValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.Search).MaximumLength(150);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class SearchAdditionalGroupsQueryValidator : AbstractValidator<SearchAdditionalGroupsQuery>
{
    public SearchAdditionalGroupsQueryValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.Search).MaximumLength(150);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
