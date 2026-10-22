using FluentValidation;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Delivery;
using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Identity.Management;
using ZFome.Application.Tenancy;
using ZFome.Domain.Delivery;
using ZFome.Domain.SharedKernel;

namespace ZFome.Application.Delivery;

public sealed record UpsertDeliveryRegionCommand(Guid EstablishmentId, Guid? RegionId, string Name,
    string PostalCodeFrom, string PostalCodeTo, decimal Fee, int EstimatedMinutes) : ICommand<Guid>;
public sealed record SetDeliveryRegionActiveCommand(Guid EstablishmentId, Guid RegionId, bool IsActive) : ICommand;
public sealed record ListDeliveryRegionsQuery(Guid EstablishmentId) : IQuery<IReadOnlyList<DeliveryRegionReadModel>>;

public sealed class UpsertDeliveryRegionHandler(AdministrativeUserManagement management, IDeliveryRegionRepository repository, IDeliveryReadGateway reads, TimeProvider time)
    : ICommandHandler<UpsertDeliveryRegionCommand, Guid>
{
    public async Task<Guid> HandleAsync(UpsertDeliveryRegionCommand command, CancellationToken ct)
    {
        Guid result = Guid.Empty;
        await management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
        {
            var now = time.GetUtcNow();
            var fee = new Money(command.Fee);
            var from = DeliveryRegion.NormalizePostalCode(command.PostalCodeFrom);
            var to = DeliveryRegion.NormalizePostalCode(command.PostalCodeTo);
            var regions = await reads.ListAsync(scope.TenantId, scope.EstablishmentId, token);
            if (regions.Any(x => x.IsActive && x.Id != command.RegionId && from.CompareTo(x.PostalCodeTo) <= 0 && to.CompareTo(x.PostalCodeFrom) >= 0))
                throw new Application.Exceptions.ConflictException("The postal code range overlaps another active delivery region.");
            if (command.RegionId is { } id)
            {
                var region = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, id, token)
                    ?? throw new Application.Exceptions.NotFoundException("Delivery region was not found.");
                region.Update(command.Name, command.PostalCodeFrom, command.PostalCodeTo, fee, command.EstimatedMinutes, now);
                result = region.Id;
            }
            else
            {
                var region = DeliveryRegion.Create(scope.TenantId, scope.EstablishmentId, command.Name,
                    command.PostalCodeFrom, command.PostalCodeTo, fee, command.EstimatedMinutes, now);
                await repository.AddAsync(region, token);
                result = region.Id;
            }
            await repository.SaveChangesAsync(token);
        }, ct);
        return result;
    }
}

public sealed class SetDeliveryRegionActiveHandler(AdministrativeUserManagement management, IDeliveryRegionRepository repository, IDeliveryReadGateway reads, TimeProvider time)
    : ICommandHandler<SetDeliveryRegionActiveCommand>
{
    public Task HandleAsync(SetDeliveryRegionActiveCommand command, CancellationToken ct) => management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
    {
        var region = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, command.RegionId, token)
            ?? throw new Application.Exceptions.NotFoundException("Delivery region was not found.");
        if (command.IsActive)
        {
            var regions = await reads.ListAsync(scope.TenantId, scope.EstablishmentId, token);
            if (regions.Any(x => x.IsActive && x.Id != region.Id && region.PostalCodeFrom.CompareTo(x.PostalCodeTo) <= 0 && region.PostalCodeTo.CompareTo(x.PostalCodeFrom) >= 0))
                throw new Application.Exceptions.ConflictException("The postal code range overlaps another active delivery region.");
        }
        region.SetActive(command.IsActive, time.GetUtcNow());
        await repository.SaveChangesAsync(token);
    }, ct);
}

public sealed class ListDeliveryRegionsHandler(EstablishmentScopeResolver scopes, ZFome.Application.Abstractions.Tenancy.ITenantContext tenantContext,
    ZFome.Application.Abstractions.Identity.IAdministrativeUserReadGateway users, IDeliveryReadGateway gateway)
    : IQueryHandler<ListDeliveryRegionsQuery, IReadOnlyList<DeliveryRegionReadModel>>
{
    public async Task<IReadOnlyList<DeliveryRegionReadModel>> HandleAsync(ListDeliveryRegionsQuery query, CancellationToken ct)
    {
        var scope = await scopes.ResolveAsync(query.EstablishmentId, ct);
        if (!await users.CanManageAsync(scope.TenantId, scope.UserId, tenantContext.IsPlatformUser, ct))
            throw new Application.Exceptions.ForbiddenException("Unit administration is not permitted.");
        return await gateway.ListAsync(scope.TenantId, query.EstablishmentId, ct);
    }
}

public sealed class UpsertDeliveryRegionValidator : AbstractValidator<UpsertDeliveryRegionCommand>
{
    public UpsertDeliveryRegionValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.RegionId).NotEqual(Guid.Empty).When(x => x.RegionId.HasValue);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostalCodeFrom).NotEmpty().Matches("^[0-9.\\- ]{8,12}$");
        RuleFor(x => x.PostalCodeTo).NotEmpty().Matches("^[0-9.\\- ]{8,12}$");
        RuleFor(x => x.Fee).GreaterThanOrEqualTo(0).LessThanOrEqualTo(100000);
        RuleFor(x => x.EstimatedMinutes).InclusiveBetween(1, 1440);
    }
}

public sealed class SetDeliveryRegionActiveValidator : AbstractValidator<SetDeliveryRegionActiveCommand>
{
    public SetDeliveryRegionActiveValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.RegionId).NotEmpty(); }
}

public sealed class ListDeliveryRegionsValidator : AbstractValidator<ListDeliveryRegionsQuery>
{
    public ListDeliveryRegionsValidator() => RuleFor(x => x.EstablishmentId).NotEmpty();
}
