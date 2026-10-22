using FluentValidation;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Tenancy;

namespace ZFome.Application.Ordering;

public sealed record GetKitchenQueueQuery(Guid EstablishmentId)
    : IQuery<IReadOnlyList<KitchenTicketReadModel>>;

public sealed class GetKitchenQueueQueryValidator : AbstractValidator<GetKitchenQueueQuery>
{
    public GetKitchenQueueQueryValidator() =>
        RuleFor(query => query.EstablishmentId).NotEmpty();
}

public sealed class GetKitchenQueueQueryHandler(
    EstablishmentScopeResolver scopeResolver,
    IKitchenDisplayReadGateway gateway)
    : IQueryHandler<GetKitchenQueueQuery, IReadOnlyList<KitchenTicketReadModel>>
{
    public async Task<IReadOnlyList<KitchenTicketReadModel>> HandleAsync(
        GetKitchenQueueQuery query,
        CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(query.EstablishmentId, cancellationToken);
        return await gateway.GetQueueAsync(
            scope.TenantId,
            scope.EstablishmentId,
            cancellationToken);
    }
}
