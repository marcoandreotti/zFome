using FluentValidation;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.PublicOrdering;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Exceptions;
using ZFome.Application.Identity.Management;
using ZFome.Application.Tenancy;
using ZFome.Domain.Ordering;

namespace ZFome.Application.Ordering;

public sealed record SetOrderSchedulingPolicyCommand(
    Guid EstablishmentId,
    OrderServiceType ServiceType,
    bool IsEnabled,
    int MinimumAdvanceMinutes,
    int HorizonDays,
    int? MaximumOrdersPerSlot) : ICommand;

public sealed record GetOrderSchedulingConfigurationQuery(Guid EstablishmentId) : IQuery<OrderSchedulingConfigurationReadModel>;
public sealed record GetPublicOrderSchedulingSlotsQuery(string Slug, OrderServiceType ServiceType) : IQuery<OrderSchedulingSlotsReadModel>;

public sealed class SetOrderSchedulingPolicyCommandHandler(AdministrativeUserManagement management, IOrderSchedulingRepository repository) : ICommandHandler<SetOrderSchedulingPolicyCommand>
{
    public Task HandleAsync(SetOrderSchedulingPolicyCommand command, CancellationToken cancellationToken) =>
        management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
        {
            var policy = await repository.GetPolicyAsync(scope.TenantId, scope.EstablishmentId, command.ServiceType, token);
            if (policy is null)
                repository.Add(OrderSchedulingPolicy.Create(scope.TenantId, scope.EstablishmentId, command.ServiceType, command.IsEnabled,
                    command.MinimumAdvanceMinutes, command.HorizonDays, command.MaximumOrdersPerSlot));
            else
                policy.Configure(command.IsEnabled, command.MinimumAdvanceMinutes, command.HorizonDays, command.MaximumOrdersPerSlot);
            await repository.SaveChangesAsync(token);
        }, cancellationToken);
}

public sealed class GetOrderSchedulingConfigurationQueryHandler(EstablishmentScopeResolver scopeResolver, IOrderSchedulingReadGateway gateway) : IQueryHandler<GetOrderSchedulingConfigurationQuery, OrderSchedulingConfigurationReadModel>
{
    public async Task<OrderSchedulingConfigurationReadModel> HandleAsync(GetOrderSchedulingConfigurationQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(query.EstablishmentId, cancellationToken);
        return await gateway.GetConfigurationAsync(scope.TenantId, scope.EstablishmentId, cancellationToken);
    }
}

public sealed class GetPublicOrderSchedulingSlotsQueryHandler(IPublicOrderingContextGateway contexts, IOrderSchedulingReadGateway gateway, TimeProvider timeProvider) : IQueryHandler<GetPublicOrderSchedulingSlotsQuery, OrderSchedulingSlotsReadModel>
{
    public async Task<OrderSchedulingSlotsReadModel> HandleAsync(GetPublicOrderSchedulingSlotsQuery query, CancellationToken cancellationToken)
    {
        var context = await contexts.ResolveAsync(query.Slug.Trim().ToLowerInvariant(), null, cancellationToken)
            ?? throw new NotFoundException("Public ordering context was not found.");
        return await gateway.GetPublicSlotsAsync(context.TenantId, context.EstablishmentId, query.ServiceType, timeProvider.GetUtcNow(), cancellationToken);
    }
}

public sealed class SetOrderSchedulingPolicyCommandValidator : AbstractValidator<SetOrderSchedulingPolicyCommand>
{
    public SetOrderSchedulingPolicyCommandValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty();
        RuleFor(x => x.ServiceType).Must(x => x is OrderServiceType.Pickup or OrderServiceType.Delivery);
        RuleFor(x => x.MinimumAdvanceMinutes).InclusiveBetween(0, OrderSchedulingPolicy.MaximumHorizonDays * 24 * 60);
        RuleFor(x => x.HorizonDays).InclusiveBetween(1, OrderSchedulingPolicy.MaximumHorizonDays);
        RuleFor(x => x).Must(x => x.MinimumAdvanceMinutes <= x.HorizonDays * 24 * 60)
            .WithMessage("O prazo mínimo de antecedência não pode exceder o horizonte de agendamento.");
        RuleFor(x => x.MaximumOrdersPerSlot).GreaterThan(0).When(x => x.MaximumOrdersPerSlot.HasValue);
    }
}

public sealed class GetOrderSchedulingConfigurationQueryValidator : AbstractValidator<GetOrderSchedulingConfigurationQuery>
{
    public GetOrderSchedulingConfigurationQueryValidator() => RuleFor(x => x.EstablishmentId).NotEmpty();
}

public sealed class GetPublicOrderSchedulingSlotsQueryValidator : AbstractValidator<GetPublicOrderSchedulingSlotsQuery>
{
    public GetPublicOrderSchedulingSlotsQueryValidator()
    {
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ServiceType).Must(x => x is OrderServiceType.Pickup or OrderServiceType.Delivery);
    }
}
