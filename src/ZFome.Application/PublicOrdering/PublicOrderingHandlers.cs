using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Customers;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Payments;
using ZFome.Application.Abstractions.Promotions;
using ZFome.Application.Abstractions.PublicOrdering;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Exceptions;
using ZFome.Domain.Customers;
using ZFome.Domain.Ordering;
using ZFome.Domain.SharedKernel;
using ZFome.Application.Abstractions.Operations;
using ZFome.Domain.Operations;
using ZFome.Application.Abstractions.Delivery;
using ZFome.Application.Abstractions.Communications;
using ZFome.Domain.Delivery;

namespace ZFome.Application.PublicOrdering;

public sealed class GetPublicContextQueryHandler(IPublicOrderingContextGateway gateway, IAvailabilityReadGateway availability, TimeProvider timeProvider) : IQueryHandler<GetPublicContextQuery, PublicOrderingContext>
{
    public async Task<PublicOrderingContext> HandleAsync(GetPublicContextQuery query, CancellationToken cancellationToken)
    {
        var context = await gateway.ResolveAsync(NormalizeSlug(query.Slug), NormalizeOptional(query.TableToken), cancellationToken)
            ?? throw new NotFoundException("Public ordering context was not found.");
        var now = timeProvider.GetUtcNow();
        var decisions = new List<PublicServiceAvailability>();
        foreach (var serviceType in Enum.GetValues<OrderServiceType>())
        {
            var decision = await availability.EvaluateAsync(context.TenantId, context.EstablishmentId, serviceType, now, cancellationToken);
            decisions.Add(new(serviceType, decision.IsAvailable, decision.Reason, decision.Message, decision.NextOpening));
        }
        return context with { Availability = decisions };
    }
    internal static string NormalizeSlug(string value) => value.Trim().ToLowerInvariant();
    internal static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class UpsertPublicCustomerCommandHandler(IPublicOrderingContextGateway contexts, ICustomerRepository customers, TimeProvider timeProvider) : ICommandHandler<UpsertPublicCustomerCommand, PublicCustomerResult>
{
    public async Task<PublicCustomerResult> HandleAsync(UpsertPublicCustomerCommand command, CancellationToken cancellationToken)
    {
        var scope = await contexts.ResolveAsync(GetPublicContextQueryHandler.NormalizeSlug(command.Slug), null, cancellationToken) ?? throw new NotFoundException("Public ordering context was not found.");
        var normalizedPhone = Customer.NormalizePhone(command.Phone);
        var customer = await customers.FindByNormalizedPhoneAsync(scope.TenantId, scope.EstablishmentId, normalizedPhone, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (customer is null) { customer = Customer.Create(scope.TenantId, scope.EstablishmentId, command.Name, command.Phone, command.Email, now); await customers.AddAsync(customer, cancellationToken); }
        else { customer.UpdateContact(command.Name, command.Phone, command.Email, now); }
        Guid? addressId = null;
        if (command.Address is { } address)
        {
            var existing = customer.Addresses.FirstOrDefault(x => x.Label.Equals(address.Label.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existing is null) addressId = customer.AddAddress(address.Label, address.Street, address.Number, address.Complement, address.Neighborhood, address.City, address.State, address.PostalCode, true, now).Id;
            else { customer.UpdateAddress(existing.Id, address.Label, address.Street, address.Number, address.Complement, address.Neighborhood, address.City, address.State, address.PostalCode, true, now); addressId = existing.Id; }
        }
        await customers.SaveChangesAsync(cancellationToken);
        return new(customer.Id, addressId);
    }
}

public sealed class SimulatePublicOrderQueryHandler(IPublicOrderingContextGateway contexts, IOrderOfferResolver offers, IOrderCustomerResolver customers, IOrderTableResolver tables, ICouponRepository coupons, IPaymentMethodRepository paymentMethods, IAvailabilityReadGateway availability, IDeliveryReadGateway delivery, IOrderSchedulingRepository scheduling, TimeProvider timeProvider) : IQueryHandler<SimulatePublicOrderQuery, PublicSimulation>
{
    public async Task<PublicSimulation> HandleAsync(SimulatePublicOrderQuery query, CancellationToken cancellationToken)
    {
        var scope = await PublicOrderComposer.ResolveScopeAsync(contexts, query.Slug, query.TableToken, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var decision = await availability.EvaluateAsync(scope.TenantId, scope.EstablishmentId, query.ServiceType, query.ScheduledAtUtc ?? now, cancellationToken);
        if (!decision.IsAvailable) throw new AvailabilityConflictException(decision.Message ?? "Service is unavailable.", decision.Reason, decision.NextOpening);
        string? timeZoneId = null;
        if (query.ScheduledAtUtc is { } scheduledAt)
        {
            var policy = await scheduling.GetPolicyAsync(scope.TenantId, scope.EstablishmentId, query.ServiceType, cancellationToken);
            timeZoneId = await scheduling.GetTimeZoneIdAsync(scope.TenantId, scope.EstablishmentId, cancellationToken);
            if (policy is null || timeZoneId is null) throw new ConflictException("Scheduling is not configured for this service.");
            var endDecision = await availability.EvaluateAsync(scope.TenantId, scope.EstablishmentId, query.ServiceType, scheduledAt.AddMinutes(policy.SlotIntervalMinutes).AddTicks(-1), cancellationToken);
            var reserved = await scheduling.CountReservedOrdersAsync(scope.TenantId, scope.EstablishmentId, query.ServiceType, scheduledAt, cancellationToken);
            try { OrderSchedulingEvaluator.EnsureSlotIsValid(policy, timeZoneId, scheduledAt, now, reserved, decision.IsAvailable && endDecision.IsAvailable); }
            catch (ZFome.Domain.Exceptions.DomainException exception) { throw new ConflictException(exception.Message); }
        }
        if (query.PaymentMethodId is { } methodId && (await paymentMethods.GetAsync(scope.TenantId, scope.EstablishmentId, methodId, cancellationToken) is not { IsActive: true })) throw new ConflictException("Payment method is not available.");
        var order = await PublicOrderComposer.ComposeAsync(scope, query.ServiceType, query.CustomerId, query.CustomerAddressId, query.DeliveryAddress, query.Items, offers, customers, tables, now, cancellationToken);
        var quote = await PublicOrderComposer.ApplyDeliveryQuoteAsync(order, scope, delivery, now, cancellationToken);
        await PublicOrderComposer.ApplyCouponAsync(order, query.CouponCode, scope, coupons, now, cancellationToken);
        return PublicOrderComposer.ToSimulation(order, quote, query.ServiceType == OrderServiceType.Delivery ? now : null, query.ScheduledAtUtc, timeZoneId);
    }
}

public sealed class ConfirmPublicOrderCommandHandler(IPublicOrderingContextGateway contexts, IOrderOfferResolver offers, IOrderCustomerResolver customers, IOrderTableResolver tables, ICouponRepository coupons, IPaymentMethodRepository paymentMethods, IPaymentRepository payments, IOrderRepository orders, IOrderNumberSequence sequence, IPublicOrderRequestRepository requests, IPublicOrderTransaction transaction, IOrderAvailabilityGateway availability, IDeliveryReadGateway delivery, IOrderSchedulingRepository scheduling, ICustomerOrderNotificationStager customerNotifications, IOrderUpdatePublisher updatePublisher, TimeProvider timeProvider) : ICommandHandler<ConfirmPublicOrderCommand, PublicConfirmation>
{
    public async Task<PublicConfirmation> HandleAsync(ConfirmPublicOrderCommand command, CancellationToken cancellationToken)
    {
        var scope = await PublicOrderComposer.ResolveScopeAsync(contexts, command.Slug, command.TableToken, cancellationToken);
        var hash = Hash(command);
        var previous = await requests.FindAsync(scope.TenantId, scope.EstablishmentId, command.IdempotencyKey.Trim(), cancellationToken);
        if (previous is not null) return await ReplayAsync(previous, hash, scope, cancellationToken);
        OrderUpdateSignal? signal = null;
        var result = await transaction.ExecuteAsync(async token =>
        {
            previous = await requests.FindAsync(scope.TenantId, scope.EstablishmentId, command.IdempotencyKey.Trim(), token);
            if (previous is not null) return await ReplayAsync(previous, hash, scope, token);
            var method = await paymentMethods.GetAsync(scope.TenantId, scope.EstablishmentId, command.PaymentMethodId, token);
            if (method is not { IsActive: true }) throw new ConflictException("Payment method is not available.");
            var now = timeProvider.GetUtcNow();
            var decision = await availability.EvaluateAsync(scope.TenantId, scope.EstablishmentId, command.ServiceType, command.ScheduledAtUtc ?? now, token);
            if (!decision.IsAvailable) throw new AvailabilityConflictException(decision.Message ?? "Service is unavailable.", decision.Reason, decision.NextOpening);
            var order = await PublicOrderComposer.ComposeAsync(scope, command.ServiceType, command.CustomerId, command.CustomerAddressId, command.DeliveryAddress, command.Items, offers, customers, tables, now, token);
            if (command.ScheduledAtUtc is { } scheduledAt)
            {
                var policy = await scheduling.GetPolicyAsync(scope.TenantId, scope.EstablishmentId, command.ServiceType, token);
                var timeZoneId = await scheduling.GetTimeZoneIdAsync(scope.TenantId, scope.EstablishmentId, token);
                if (policy is null || timeZoneId is null) throw new ConflictException("Scheduling is not configured for this service.");
                var startDecision = await availability.EvaluateAsync(scope.TenantId, scope.EstablishmentId, command.ServiceType, scheduledAt, token);
                var endDecision = await availability.EvaluateAsync(scope.TenantId, scope.EstablishmentId, command.ServiceType, scheduledAt.AddMinutes(policy.SlotIntervalMinutes).AddTicks(-1), token);
                var reserved = await scheduling.CountReservedOrdersAsync(scope.TenantId, scope.EstablishmentId, command.ServiceType, scheduledAt, token);
                try { OrderSchedulingEvaluator.EnsureSlotIsValid(policy, timeZoneId, scheduledAt, now, reserved, startDecision.IsAvailable && endDecision.IsAvailable); order.Schedule(scheduledAt, timeZoneId, now); }
                catch (ZFome.Domain.Exceptions.DomainException exception) { throw new ConflictException(exception.Message); }
            }
            var quote = await PublicOrderComposer.ApplyDeliveryQuoteAsync(order, scope, delivery, now, token);
            var coupon = await PublicOrderComposer.ApplyCouponAsync(order, command.CouponCode, scope, coupons, now, token);
            if (command.ServiceType == OrderServiceType.Delivery &&
                (quote is null || command.DeliveryRegionId != quote.RegionId || command.ExpectedDeliveryFee != quote.Fee.Amount ||
                 command.ExpectedDeliveryEstimatedMinutes != quote.EstimatedMinutes || command.DeliveryQuoteIssuedAt is not { } issuedAt ||
                 issuedAt > now || now - issuedAt > TimeSpan.FromMinutes(10)))
                throw new DeliveryQuoteConflictException("Delivery quote changed or expired. Review the updated delivery total before confirming.", quote?.Fee.Amount ?? 0, order.Total.Amount);
            var number = await sequence.ReserveAsync(scope.TenantId, scope.EstablishmentId, token); order.Confirm(number, now);
            await orders.AddAsync(order, token);
            coupon?.Consume(order.Id, order.Subtotal, now);
            if (coupon is not null) await coupons.SaveChangesAsync(token);
            await payments.AddAsync(Domain.Payments.Payment.Create(scope.TenantId, scope.EstablishmentId, order.Id, method, order.Total, command.ReceivedAmount is null ? null : new Money(command.ReceivedAmount.Value), now), token);
            await requests.AddAsync(PublicOrderRequest.Create(scope.TenantId, scope.EstablishmentId, command.IdempotencyKey, hash, order.Id, now), token);
            await customerNotifications.StageAsync(order, token);
            await orders.SaveChangesAsync(token);
            signal = new OrderUpdateSignal(scope.EstablishmentId, order.Id, OrderUpdateKind.Confirmed, now);
            return new PublicConfirmation(order.PublicReference!, order.Number!.Value, order.Status, order.Total.Amount);
        }, cancellationToken);
        if (signal is not null)
            await updatePublisher.PublishAsync(scope.TenantId, signal, cancellationToken);
        return result;
    }

    private async Task<PublicConfirmation> ReplayAsync(PublicOrderRequest request, string hash, PublicOrderingContext scope, CancellationToken token)
    {
        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(request.PayloadHash), Convert.FromHexString(hash))) throw new ConflictException("Idempotency key was already used with different content.");
        var order = await orders.GetAsync(scope.TenantId, scope.EstablishmentId, request.OrderId, token) ?? throw new ConflictException("Idempotent order result is unavailable.");
        return new(order.PublicReference!, order.Number!.Value, order.Status, order.Total.Amount);
    }

    private static string Hash(ConfirmPublicOrderCommand value)
    {
        var content = string.Join('|', value.Slug.Trim().ToLowerInvariant(), value.ServiceType, value.CustomerId, value.CustomerAddressId, value.TableToken?.Trim(), Address(value.DeliveryAddress), value.CouponCode?.Trim().ToUpperInvariant(), value.PaymentMethodId, value.ReceivedAmount?.ToString(CultureInfo.InvariantCulture), value.DeliveryRegionId, value.ExpectedDeliveryFee?.ToString(CultureInfo.InvariantCulture), value.ExpectedDeliveryEstimatedMinutes, value.DeliveryQuoteIssuedAt?.ToString("O"), value.ScheduledAtUtc?.ToUniversalTime().ToString("O"), string.Join(';', value.Items.Select(x => $"{x.ProductId},{x.VariationId},{x.Quantity.ToString(CultureInfo.InvariantCulture)},{x.Notes},{string.Join(',', x.Additionals.OrderBy(a => a.GroupId).ThenBy(a => a.AdditionalId).Select(a => $"{a.GroupId}:{a.AdditionalId}:{a.Quantity.ToString(CultureInfo.InvariantCulture)}:{a.PortionNumerator}/{a.PortionDenominator}"))}")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }
    private static string Address(PublicAddress? x) => x is null ? string.Empty : $"{x.Label},{x.Street},{x.Number},{x.Complement},{x.Neighborhood},{x.City},{x.State},{x.PostalCode}";
}

public sealed class GetPublicOrderQueryHandler(IPublicOrderLocator locator, IOrderReadGateway orders) : IQueryHandler<GetPublicOrderQuery, OrderReadModel>
{
    public async Task<OrderReadModel> HandleAsync(GetPublicOrderQuery query, CancellationToken cancellationToken)
    { var location = await locator.FindAsync(query.Reference, cancellationToken) ?? throw new NotFoundException("Order was not found."); return await orders.GetAsync(location.TenantId, location.EstablishmentId, location.OrderId, cancellationToken) ?? throw new NotFoundException("Order was not found."); }
}

public sealed class CancelPublicOrderCommandHandler(IPublicOrderLocator locator, IOrderRepository orders, ICustomerOrderNotificationStager customerNotifications, IOrderUpdatePublisher updatePublisher, TimeProvider timeProvider) : ICommandHandler<CancelPublicOrderCommand>
{
    public async Task HandleAsync(CancelPublicOrderCommand command, CancellationToken cancellationToken)
    { var location = await locator.FindAsync(command.Reference, cancellationToken) ?? throw new NotFoundException("Order was not found."); var order = await orders.GetAsync(location.TenantId, location.EstablishmentId, location.OrderId, cancellationToken) ?? throw new NotFoundException("Order was not found."); if(order.Status!=OrderStatus.Confirmed) throw new Domain.Exceptions.DomainException("Order can no longer be cancelled by the customer."); var now = timeProvider.GetUtcNow(); order.Cancel(now, null, command.Reason); await customerNotifications.StageAsync(order, cancellationToken); await orders.SaveChangesAsync(cancellationToken); await updatePublisher.PublishAsync(location.TenantId, new OrderUpdateSignal(location.EstablishmentId, order.Id, OrderUpdateKind.StatusChanged, now), cancellationToken); }
}

internal static class PublicOrderComposer
{
    public static async Task<PublicOrderingContext> ResolveScopeAsync(IPublicOrderingContextGateway contexts, string slug, string? tableToken, CancellationToken token) => await contexts.ResolveAsync(GetPublicContextQueryHandler.NormalizeSlug(slug), GetPublicContextQueryHandler.NormalizeOptional(tableToken), token) ?? throw new NotFoundException("Public ordering context was not found.");
    public static async Task<Order> ComposeAsync(PublicOrderingContext scope, OrderServiceType serviceType, Guid? customerId, Guid? addressId, PublicAddress? address, IReadOnlyCollection<PublicOrderLine> lines, IOrderOfferResolver offers, IOrderCustomerResolver customers, IOrderTableResolver tables, DateTimeOffset now, CancellationToken token)
    {
        OrderCustomerSnapshot? customer = null; if (customerId is { } id) customer = await customers.ResolveAsync(scope.TenantId, scope.EstablishmentId, id, addressId, token) ?? throw new NotFoundException("Customer was not found.");
        Guid? tableId = null; if (serviceType == OrderServiceType.Table) { tableId = scope.TableId; if (tableId is null || await tables.ResolveActiveAsync(scope.TenantId, scope.EstablishmentId, tableId.Value, token) is null) throw new NotFoundException("Active table was not found."); }
        var delivery = address is null ? customer?.DeliveryAddress : new DeliveryAddressSnapshot(address.Street, address.Number, address.Complement, address.Neighborhood, address.City, address.State, address.PostalCode);
        var order = Order.Create(scope.TenantId, scope.EstablishmentId, serviceType, customer?.CustomerId, customer?.Name, customer?.Phone, tableId, delivery, now);
        foreach (var line in lines) { var offer = await offers.ResolveAsync(scope.TenantId, scope.EstablishmentId, line.ProductId, line.VariationId, line.Additionals, now, token) ?? throw new AvailabilityConflictException("One or more offers are unavailable.", AvailabilityReason.OfferUnavailable, null, [line.ProductId]); if (offer.Composition is { } composition) order.AddComposedItem(offer.ProductId, offer.VariationId, offer.ProductName, offer.VariationName, composition, new Quantity(line.Quantity), line.Notes, now); else order.AddItem(offer.ProductId, offer.VariationId, offer.ProductName, offer.VariationName, offer.UnitPrice, new Quantity(line.Quantity), offer.Additionals.Select(x => new OrderAdditionalInput(x.AdditionalId, x.Name, x.UnitPrice, x.Quantity)).ToArray(), line.Notes, now); }
        return order;
    }
    public static async Task<DeliveryQuote?> ApplyDeliveryQuoteAsync(Order order, PublicOrderingContext scope, IDeliveryReadGateway delivery, DateTimeOffset now, CancellationToken token)
    {
        if (order.ServiceType != OrderServiceType.Delivery) return null;
        if (order.DeliveryAddress is null) throw new ConflictException("Delivery is unavailable for this address.");
        var postalCode = DeliveryRegion.NormalizePostalCode(order.DeliveryAddress.PostalCode);
        var quote = await delivery.FindQuoteAsync(scope.TenantId, scope.EstablishmentId, postalCode, token)
            ?? throw new ConflictException("Delivery is unavailable for this address.");
        order.ApplyDeliveryQuote(quote, now);
        return quote;
    }
    public static async Task<Domain.Promotions.Coupon?> ApplyCouponAsync(Order order, string? code, PublicOrderingContext scope, ICouponRepository coupons, DateTimeOffset now, CancellationToken token)
    { if (string.IsNullOrWhiteSpace(code)) return null; var coupon = await coupons.FindByCodeAsync(scope.TenantId, scope.EstablishmentId, Domain.Promotions.Coupon.NormalizeCode(code), token) ?? throw new NotFoundException("Coupon was not found."); var evaluation = coupon.Evaluate(order.Subtotal, now); order.ApplyCoupon(evaluation.CouponId, evaluation.Code, evaluation.Discount, now); return coupon; }
    public static PublicSimulation ToSimulation(Order order, DeliveryQuote? quote, DateTimeOffset? issuedAt, DateTimeOffset? scheduledAtUtc = null, string? timeZoneId = null) => new(order.Subtotal.Amount, order.Discount.Amount, order.Fees.Amount, order.Total.Amount, order.CouponCode, order.Items.Select(x => new PublicSimulationItem(x.ProductName, x.VariationName, x.UnitPrice.Amount, x.Quantity.Value, x.Total.Amount, x.Additionals.Select(a => new PublicSimulationAdditional(a.Name, a.UnitPrice.Amount, a.Quantity.Value)).ToArray(), x.BasePrice.Amount, x.ModifierGroups.Select(g => new OrderModifierGroupReadModel(g.ModifierGroupId, g.Name, g.PricingStrategy.ToString(), g.Price.Amount, g.Options.Select(o => new OrderModifierOptionReadModel(o.ModifierOptionId, o.Name, o.UnitPrice.Amount, o.Quantity.Value, o.PortionNumerator, o.PortionDenominator)).ToArray())).ToArray())).ToArray(), quote?.RegionId, quote?.RegionName, quote?.Fee.Amount, quote?.EstimatedMinutes, issuedAt, scheduledAtUtc?.ToUniversalTime(), timeZoneId);
}
