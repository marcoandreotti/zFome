using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Exceptions;
using ZFome.Application.Tenancy;
using ZFome.Domain.Ordering;
using ZFome.Domain.SharedKernel;
using FluentValidation.Results;
using ZFome.Application.Abstractions.Promotions;
using ZFome.Application.Abstractions.Communications;
using ZFome.Domain.Promotions;
using ZFome.Domain.Operations;
using ZFome.Domain.Exceptions;

namespace ZFome.Application.Ordering;

public sealed class CreateOrderDraftCommandHandler(
    EstablishmentScopeResolver scopeResolver,
    IOrderRepository repository,
    IOrderCustomerResolver customerResolver,
    IOrderTableResolver tableResolver,
    TimeProvider timeProvider) : ICommandHandler<CreateOrderDraftCommand, Guid>
{
    public async Task<Guid> HandleAsync(CreateOrderDraftCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken);
        OrderCustomerSnapshot? customer = null;
        if (command.CustomerId is { } customerId)
        {
            customer = await customerResolver.ResolveAsync(scope.TenantId, scope.EstablishmentId, customerId, command.CustomerAddressId, cancellationToken)
                ?? throw new NotFoundException("Customer was not found in this establishment.");
        }

        if (command.ServiceType == OrderServiceType.Table)
        {
            if (command.TableId is not { } tableId || await tableResolver.ResolveActiveAsync(scope.TenantId, scope.EstablishmentId, tableId, cancellationToken) is null)
                throw new NotFoundException("Active table was not found in this establishment.");
        }

        var address = command.DeliveryAddress is null
            ? customer?.DeliveryAddress
            : new DeliveryAddressSnapshot(command.DeliveryAddress.Street, command.DeliveryAddress.Number, command.DeliveryAddress.Complement, command.DeliveryAddress.Neighborhood, command.DeliveryAddress.City, command.DeliveryAddress.State, command.DeliveryAddress.PostalCode);
        var order = Order.Create(scope.TenantId, scope.EstablishmentId, command.ServiceType, customer?.CustomerId, customer?.Name, customer?.Phone, command.TableId, address, timeProvider.GetUtcNow());
        await repository.AddAsync(order, cancellationToken);
        return order.Id;
    }
}

public sealed class AddOrderItemCommandHandler(
    EstablishmentScopeResolver scopeResolver,
    IOrderRepository repository,
    IOrderOfferResolver offerResolver,
    TimeProvider timeProvider) : ICommandHandler<AddOrderItemCommand, Guid>
{
    public async Task<Guid> HandleAsync(AddOrderItemCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken);
        var order = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order was not found.");
        var offer = await offerResolver.ResolveAsync(scope.TenantId, scope.EstablishmentId, command.ProductId, command.VariationId, command.Additionals, timeProvider.GetUtcNow(), cancellationToken)
            ?? throw new NotFoundException("Sellable offer was not found in this establishment.");
        var item = offer.Composition is { } composition
            ? order.AddComposedItem(offer.ProductId, offer.VariationId, offer.ProductName, offer.VariationName, composition, new Quantity(command.Quantity), command.Notes, timeProvider.GetUtcNow())
            : order.AddItem(offer.ProductId, offer.VariationId, offer.ProductName, offer.VariationName, offer.UnitPrice, new Quantity(command.Quantity),
                offer.Additionals.Select(x => new OrderAdditionalInput(x.AdditionalId, x.Name, x.UnitPrice, x.Quantity)).ToArray(), command.Notes, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return item.Id;
    }
}

public sealed class ConfirmOrderCommandHandler(
    EstablishmentScopeResolver scopeResolver,
    IOrderRepository repository,
    IOrderOfferResolver offerResolver,
    IOrderNumberSequence sequence,
    IOrderConfirmationTransaction transaction,
    ICouponRepository coupons,
    IOrderAvailabilityGateway availability,
    IOrderUpdatePublisher updatePublisher,
    TimeProvider timeProvider) : ICommandHandler<ConfirmOrderCommand>
{
    public async Task HandleAsync(ConfirmOrderCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken);
        var order = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order was not found.");

        foreach (var item in order.Items)
        {
            var selections = item.ModifierGroups.SelectMany(g => g.Options.Select(o => new OrderAdditionalSelection(o.ModifierOptionId, o.Quantity.Value, g.ModifierGroupId, o.PortionNumerator, o.PortionDenominator))).ToArray();
            if (selections.Length == 0) selections = item.Additionals.Select(x => new OrderAdditionalSelection(x.AdditionalId, x.Quantity.Value)).ToArray();
            var current = await offerResolver.ResolveAsync(scope.TenantId, scope.EstablishmentId, item.ProductId, item.VariationId, selections, timeProvider.GetUtcNow(), cancellationToken);
            if (current is null || !Matches(item, current))
                throw new ConflictException("Order offer changed or is no longer available. Recompose the order before confirming.");
        }

        Coupon? coupon = null;
        if (order.CouponId is { } couponId)
        {
            coupon = await coupons.GetAsync(scope.TenantId, scope.EstablishmentId, couponId, cancellationToken) ?? throw new ConflictException("Applied coupon no longer exists.");
            var evaluation = coupon.Evaluate(order.Subtotal, timeProvider.GetUtcNow());
            if (evaluation.Code != order.CouponCode || evaluation.Discount != order.Discount) throw new ConflictException("Applied coupon changed. Apply it again before confirming.");
        }

        await transaction.ExecuteAsync(async token =>
        {
            var now = timeProvider.GetUtcNow();
            var decision = await availability.EvaluateAsync(scope.TenantId, scope.EstablishmentId, order.ServiceType, now, token);
            if (!decision.IsAvailable) throw new AvailabilityConflictException(decision.Message ?? "Service is unavailable.", decision.Reason, decision.NextOpening);
            foreach (var item in order.Items)
            {
                var selections = item.ModifierGroups.SelectMany(g => g.Options.Select(o => new OrderAdditionalSelection(o.ModifierOptionId, o.Quantity.Value, g.ModifierGroupId, o.PortionNumerator, o.PortionDenominator))).ToArray();
                if (selections.Length == 0) selections = item.Additionals.Select(x => new OrderAdditionalSelection(x.AdditionalId, x.Quantity.Value)).ToArray();
                var current = await offerResolver.ResolveAsync(scope.TenantId, scope.EstablishmentId, item.ProductId, item.VariationId, selections, now, token);
                if (current is null || !Matches(item, current))
                    throw new AvailabilityConflictException("Order contains unavailable or changed offers.", AvailabilityReason.OfferUnavailable, null, [item.ProductId]);
            }
            coupon?.Consume(order.Id, order.Subtotal, now);
            var number = await sequence.ReserveAsync(scope.TenantId, scope.EstablishmentId, token);
            order.Confirm(number, now, scope.UserId);
            await repository.SaveChangesAsync(token);
        }, cancellationToken);
        await updatePublisher.PublishAsync(
            scope.TenantId,
            new OrderUpdateSignal(scope.EstablishmentId, order.Id, OrderUpdateKind.Confirmed, order.UpdatedAt),
            cancellationToken);
    }

    private static bool Matches(OrderItem item, OrderOfferSnapshot current)
    {
        if (item.ProductName != current.ProductName || item.VariationName != current.VariationName) return false;
        if (current.Composition is null)
            return item.UnitPrice == current.UnitPrice && MatchesAdditionals(item, current);

        // Orders created before modifier snapshots stored the base price on the item and additions separately.
        if (item.ModifierGroups.Count == 0)
            return item.UnitPrice == current.Composition.BasePrice && MatchesAdditionals(item, current);

        return item.BasePrice == current.Composition.BasePrice && item.UnitPrice == current.Composition.UnitPrice
            && item.ModifierGroups.Count == current.Composition.Groups.Count
            && item.ModifierGroups.All(saved => current.Composition.Groups.Any(value =>
                value.GroupId == saved.ModifierGroupId && value.Name == saved.Name && value.PricingStrategy == saved.PricingStrategy
                && value.Price == saved.Price && value.Options.Count == saved.Options.Count
                && value.Options.All(option => saved.Options.Any(snapshot => snapshot.ModifierOptionId == option.OptionId
                    && snapshot.Name == option.Name && snapshot.UnitPrice == option.UnitPrice && snapshot.Quantity == option.Quantity
                    && snapshot.PortionNumerator == option.Portion?.Numerator && snapshot.PortionDenominator == option.Portion?.Denominator))));
    }

    private static bool MatchesAdditionals(OrderItem item, OrderOfferSnapshot current) =>
        item.Additionals.Count == current.Additionals.Count && item.Additionals.All(saved => current.Additionals.Any(value =>
            value.AdditionalId == saved.AdditionalId && value.Name == saved.Name && value.UnitPrice == saved.UnitPrice && value.Quantity == saved.Quantity));
}

public sealed class TransitionOrderCommandHandler(
    EstablishmentScopeResolver scopeResolver,
    IOrderRepository repository,
    IOrderSchedulingRepository scheduling,
    ICustomerOrderNotificationStager customerNotifications,
    IOrderUpdatePublisher updatePublisher,
    TimeProvider timeProvider) : ICommandHandler<TransitionOrderCommand>
{
    public async Task HandleAsync(TransitionOrderCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken);
        var order = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order was not found.");
        var now = timeProvider.GetUtcNow();
        try
        {
            switch (command.NewStatus)
            {
                case OrderStatus.Preparing:
                    if (order.ScheduledAtUtc is { } scheduledAt)
                    {
                        var policy = await scheduling.GetPolicyAsync(scope.TenantId, scope.EstablishmentId, order.ServiceType, cancellationToken);
                        var minimumAdvance = policy?.MinimumAdvanceMinutes ?? OrderSchedulingPolicy.DefaultMinimumAdvanceMinutes;
                        if (now < scheduledAt.AddMinutes(-minimumAdvance))
                            throw new ConflictException("Scheduled order is not ready for preparation yet.");
                    }
                    order.StartPreparation(now, scope.UserId);
                    break;
                case OrderStatus.Ready: order.MarkReady(now, scope.UserId); break;
                case OrderStatus.OutForDelivery: order.Dispatch(now, scope.UserId); break;
                case OrderStatus.Completed: order.Complete(now, scope.UserId); break;
                case OrderStatus.Cancelled: order.Cancel(now, scope.UserId, command.Note); break;
                case OrderStatus.Rejected: order.Reject(now, scope.UserId, command.Note); break;
                default: throw new ValidationException([new ValidationFailure(nameof(command.NewStatus), "O status solicitado não é uma transição operacional válida.")]);
            }
        }
        catch (DomainException exception)
        {
            throw new ConflictException($"Order state no longer allows this transition. {exception.Message}");
        }
        await customerNotifications.StageAsync(order, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await updatePublisher.PublishAsync(
            scope.TenantId,
            new OrderUpdateSignal(scope.EstablishmentId, order.Id, OrderUpdateKind.StatusChanged, now),
            cancellationToken);
    }
}

public sealed class GetOrderQueryHandler(EstablishmentScopeResolver scopeResolver, IOrderReadGateway gateway) : IQueryHandler<GetOrderQuery, OrderReadModel>
{
    public async Task<OrderReadModel> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(query.EstablishmentId, cancellationToken);
        return await gateway.GetAsync(scope.TenantId, scope.EstablishmentId, query.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order was not found.");
    }
}

public sealed class SearchOrdersQueryHandler(EstablishmentScopeResolver scopeResolver, IOrderReadGateway gateway) : IQueryHandler<SearchOrdersQuery, OrderSearchResult>
{
    public async Task<OrderSearchResult> HandleAsync(SearchOrdersQuery query, CancellationToken cancellationToken)
    { var scope=await scopeResolver.ResolveAsync(query.EstablishmentId,cancellationToken); return await gateway.SearchAsync(scope.TenantId,scope.EstablishmentId,query.From,query.To,query.Status,query.Number,query.ServiceType,query.Page,query.PageSize,cancellationToken); }
}
