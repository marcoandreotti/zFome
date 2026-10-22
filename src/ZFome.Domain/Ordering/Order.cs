using System.Security.Cryptography;
using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;
using ZFome.Domain.Delivery;

namespace ZFome.Domain.Ordering;

public enum OrderServiceType
{ Table, Pickup, Delivery }

public enum OrderStatus
{ Draft, Confirmed, Preparing, Ready, OutForDelivery, Completed, Cancelled, Rejected }

/// <summary>
/// Representa o snapshot do endereço de entrega de um pedido, contendo informações normalizadas e validadas.
/// </summary>
public sealed class DeliveryAddressSnapshot
{
    private DeliveryAddressSnapshot()
    { }

    public DeliveryAddressSnapshot(string street, string number, string? complement, string neighborhood, string city, string state, string postalCode)
    { Street = street; Number = number; Complement = complement; Neighborhood = neighborhood; City = city; State = state; PostalCode = postalCode; }

    public string Street { get; private set; } = string.Empty;
    public string Number { get; private set; } = string.Empty;
    public string? Complement { get; private set; }
    public string Neighborhood { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
}

public sealed record OrderAdditionalInput(Guid AdditionalId, string Name, Money UnitPrice, Quantity Quantity);

public sealed class Order : IEstablishmentScopedEntity
{
    private readonly List<OrderItem> items = [];
    private readonly List<OrderStatusHistory> history = [];

    private Order()
    { }

    private Order(
        Guid tenantId,
        Guid establishmentId,
        OrderServiceType serviceType,
        Guid? customerId,
        string? customerName,
        string? customerPhone,
        Guid? tableId,
        DeliveryAddressSnapshot? deliveryAddress,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty)
            throw new DomainException("Order scope is required.");
        if (serviceType == OrderServiceType.Table && tableId is null)
            throw new DomainException("Table service requires a table.");
        if (serviceType != OrderServiceType.Table && tableId is not null)
            throw new DomainException("Only table service can reference a table.");
        if (serviceType != OrderServiceType.Delivery && deliveryAddress is not null)
            throw new DomainException("Only delivery service can contain a delivery address.");

        Id = Guid.NewGuid();
        TenantId = tenantId;
        EstablishmentId = establishmentId;
        ServiceType = serviceType;
        CustomerId = customerId;
        CustomerName = NormalizeOptional(customerName, 150);
        CustomerPhone = NormalizeOptional(customerPhone, 30);
        TableId = tableId;
        DeliveryAddress = deliveryAddress is null ? null : Normalize(deliveryAddress);
        Status = OrderStatus.Draft;
        Version = Guid.NewGuid();
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public long? Number { get; private set; }
    public string? PublicReference { get; private set; }
    public OrderServiceType ServiceType { get; private set; }
    public OrderStatus Status { get; private set; }
    public Guid? CustomerId { get; private set; }
    public string? CustomerName { get; private set; }
    public string? CustomerPhone { get; private set; }
    public Guid? TableId { get; private set; }
    public DeliveryAddressSnapshot? DeliveryAddress { get; private set; }
    public DateTimeOffset? ScheduledAtUtc { get; private set; }
    public string? ScheduledTimeZoneId { get; private set; }
    public bool IsScheduled => ScheduledAtUtc.HasValue;
    public Guid? DeliveryRegionId { get; private set; }
    public string? DeliveryRegionName { get; private set; }
    public Money DeliveryFee { get; private set; }
    public int? DeliveryEstimatedMinutes { get; private set; }
    public Money Subtotal { get; private set; }
    public Money Discount { get; private set; }
    public Money Fees { get; private set; }
    public Money Total { get; private set; }
    public Guid? CouponId { get; private set; }
    public string? CouponCode { get; private set; }
    public Guid Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => items;
    public IReadOnlyCollection<OrderStatusHistory> History => history;

    public void Schedule(DateTimeOffset scheduledAt, string timeZoneId, DateTimeOffset now)
    {
        EnsureDraft();
        if (ServiceType is not (OrderServiceType.Pickup or OrderServiceType.Delivery) || scheduledAt <= now)
            throw new DomainException("Order scheduling is invalid for this service or time.");
        if (string.IsNullOrWhiteSpace(timeZoneId) || timeZoneId.Trim().Length > 100)
            throw new DomainException("Order scheduling time zone is invalid.");

        var normalizedTimeZone = timeZoneId.Trim();
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(normalizedTimeZone); }
        catch (TimeZoneNotFoundException) { throw new DomainException("Order scheduling time zone is invalid."); }
        catch (InvalidTimeZoneException) { throw new DomainException("Order scheduling time zone is invalid."); }

        ScheduledAtUtc = scheduledAt.ToUniversalTime();
        ScheduledTimeZoneId = normalizedTimeZone;
        Touch(now);
    }

    /// <summary>Cria um rascunho de pedido no escopo do estabelecimento.</summary>
    public static Order Create(
        Guid tenantId,
        Guid establishmentId,
        OrderServiceType serviceType,
        Guid? customerId,
        string? customerName,
        string? customerPhone,
        Guid? tableId,
        DeliveryAddressSnapshot? deliveryAddress,
        DateTimeOffset now) =>
        new(tenantId, establishmentId, serviceType, customerId, customerName, customerPhone, tableId, deliveryAddress, now);

    /// <summary>Adiciona ao rascunho um item com snapshot comercial completo.</summary>
    public OrderItem AddItem(
        Guid productId,
        Guid? variationId,
        string productName,
        string? variationName,
        Money unitPrice,
        Quantity quantity,
        IReadOnlyCollection<OrderAdditionalInput> additionals,
        string? notes,
        DateTimeOffset now)
    {
        EnsureDraft();
        if (productId == Guid.Empty) throw new DomainException("Order product is required.");
        var item = new OrderItem(TenantId, EstablishmentId, Id, productId, variationId, productName, variationName, unitPrice, quantity, additionals, notes);
        items.Add(item);
        Recalculate();
        Touch(now);
        return item;
    }

    /// <summary>Adds an item using a server-calculated modifier composition and preserves its complete price explanation.</summary>
    public OrderItem AddComposedItem(Guid productId, Guid? variationId, string productName, string? variationName,
        ModifierCompositionPrice composition, Quantity quantity, string? notes, DateTimeOffset now)
    {
        EnsureDraft();
        if (productId == Guid.Empty) throw new DomainException("Order product is required.");
        var item = new OrderItem(TenantId, EstablishmentId, Id, productId, variationId, productName, variationName,
            composition, quantity, notes);
        items.Add(item);
        Recalculate();
        Touch(now);
        return item;
    }

    /// <summary>Remove um item enquanto o pedido ainda está em composição.</summary>
    public void RemoveItem(Guid itemId, DateTimeOffset now)
    {
        EnsureDraft();
        var item = items.SingleOrDefault(x => x.Id == itemId) ?? throw new DomainException("Order item was not found.");
        items.Remove(item);
        Recalculate();
        Touch(now);
    }

    /// <summary>Define desconto e taxas e recalcula o total sem permitir resultado negativo.</summary>
    public void SetAdjustments(Money discount, Money fees, DateTimeOffset now)
    {
        EnsureDraft();
        if (discount.Amount > Subtotal.Amount + fees.Amount)
            throw new DomainException("Order total cannot be negative.");
        Discount = discount;
        Fees = fees;
        Recalculate();
        Touch(now);
    }

    /// <summary>Stores the server-calculated delivery policy snapshot on a delivery draft.</summary>
    public void ApplyDeliveryQuote(DeliveryQuote quote, DateTimeOffset now)
    {
        EnsureDraft();
        if (ServiceType != OrderServiceType.Delivery || DeliveryAddress is null)
            throw new DomainException("A delivery quote requires a delivery address.");
        if (quote.RegionId == Guid.Empty || quote.EstimatedMinutes < 1)
            throw new DomainException("Delivery quote is invalid.");
        DeliveryRegionId = quote.RegionId;
        DeliveryRegionName = quote.RegionName;
        DeliveryFee = quote.Fee;
        DeliveryEstimatedMinutes = quote.EstimatedMinutes;
        SetAdjustments(Discount, quote.Fee, now);
    }

    /// <summary>Aplica ao rascunho o snapshot do desconto calculado pelo cupom.</summary>
    public void ApplyCoupon(Guid couponId, string code, Money discount, DateTimeOffset now)
    {
        EnsureDraft();
        if (couponId == Guid.Empty || string.IsNullOrWhiteSpace(code)) throw new DomainException("Coupon snapshot is invalid.");
        CouponId = couponId; CouponCode = code; Discount = new Money(Math.Min(Subtotal.Amount + Fees.Amount, discount.Amount));
        Recalculate(); Touch(now);
    }

    /// <summary>Remove o cupom do rascunho e restaura o total sem desconto.</summary>
    public void RemoveCoupon(DateTimeOffset now)
    {
        EnsureDraft(); CouponId = null; CouponCode = null; Discount = Money.Zero; Recalculate(); Touch(now);
    }

    /// <summary>Confirma o pedido com sequência da unidade e referência pública opaca.</summary>
    public void Confirm(long number, DateTimeOffset now, Guid? actorId = null)
    {
        EnsureDraft();
        if (items.Count == 0) throw new DomainException("Order must contain at least one item.");
        if (number <= 0) throw new DomainException("Order number must be positive.");
        if (ServiceType == OrderServiceType.Delivery && DeliveryAddress is null)
            throw new DomainException("Delivery order requires an address.");
        Number = number;
        PublicReference = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        TransitionTo(OrderStatus.Confirmed, now, actorId, null);
    }

    public void StartPreparation(DateTimeOffset now, Guid actorId) => Transition(OrderStatus.Preparing, now, actorId);

    public void MarkReady(DateTimeOffset now, Guid actorId) => Transition(OrderStatus.Ready, now, actorId);

    public void Dispatch(DateTimeOffset now, Guid actorId)
    {
        if (ServiceType != OrderServiceType.Delivery) throw new DomainException("Only delivery orders can be dispatched.");
        Transition(OrderStatus.OutForDelivery, now, actorId);
    }

    public void Complete(DateTimeOffset now, Guid actorId) => Transition(OrderStatus.Completed, now, actorId);

    public void Cancel(DateTimeOffset now, Guid? actorId, string? note = null) => Transition(OrderStatus.Cancelled, now, actorId, note);

    public void Reject(DateTimeOffset now, Guid? actorId, string? note = null) => Transition(OrderStatus.Rejected, now, actorId, note);

    private void Transition(OrderStatus next, DateTimeOffset now, Guid? actorId, string? note = null)
    {
        var allowed = (Status, next, ServiceType) switch
        {
            (OrderStatus.Confirmed, OrderStatus.Preparing, _) => true,
            (OrderStatus.Preparing, OrderStatus.Ready, _) => true,
            (OrderStatus.Ready, OrderStatus.OutForDelivery, OrderServiceType.Delivery) => true,
            (OrderStatus.Ready, OrderStatus.Completed, OrderServiceType.Table or OrderServiceType.Pickup) => true,
            (OrderStatus.OutForDelivery, OrderStatus.Completed, OrderServiceType.Delivery) => true,
            (OrderStatus.Confirmed or OrderStatus.Preparing, OrderStatus.Cancelled, _) => true,
            (OrderStatus.Confirmed, OrderStatus.Rejected, _) => true,
            _ => false
        };
        if (!allowed) throw new DomainException($"Cannot transition order from {Status} to {next}.");
        TransitionTo(next, now, actorId, note);
    }

    private void TransitionTo(OrderStatus next, DateTimeOffset now, Guid? actorId, string? note)
    {
        var previous = Status;
        Status = next;
        history.Add(new OrderStatusHistory(TenantId, EstablishmentId, Id, previous, next, now, actorId, NormalizeOptional(note, 500)));
        Touch(now);
    }

    private void Recalculate()
    {
        Subtotal = new Money(items.Sum(x => x.Total.Amount));
        var beforeDiscount = Subtotal.Amount + Fees.Amount;
        if (Discount.Amount > beforeDiscount) throw new DomainException("Order total cannot be negative.");
        Total = new Money(beforeDiscount - Discount.Amount);
    }

    private void EnsureDraft()
    {
        if (Status != OrderStatus.Draft) throw new DomainException("Confirmed order composition is immutable.");
    }

    private void Touch(DateTimeOffset now)
    { UpdatedAt = now; Version = Guid.NewGuid(); }

    private static string Required(string value, int max, string field)
    { var normalized = value.Trim(); if (normalized.Length is < 1 || normalized.Length > max) throw new DomainException($"{field} is invalid."); return normalized; }

    private static string? NormalizeOptional(string? value, int max)
    { if (string.IsNullOrWhiteSpace(value)) return null; var normalized = value.Trim(); if (normalized.Length > max) throw new DomainException("Order snapshot value is too long."); return normalized; }

    private static DeliveryAddressSnapshot Normalize(DeliveryAddressSnapshot value) => new(
        Required(value.Street, 200, "Street"), Required(value.Number, 30, "Number"), NormalizeOptional(value.Complement, 100),
        Required(value.Neighborhood, 100, "Neighborhood"), Required(value.City, 100, "City"),
        Required(value.State, 2, "State").ToUpperInvariant(), Required(value.PostalCode, 12, "Postal code"));
}

public sealed class OrderItem : IEstablishmentScopedEntity
{
    private readonly List<OrderItemAdditional> additionals = [];
    private readonly List<OrderItemModifierGroup> modifierGroups = [];

    private OrderItem()
    { }

    internal OrderItem(Guid tenantId, Guid establishmentId, Guid orderId, Guid productId, Guid? variationId, string productName, string? variationName, Money unitPrice, Quantity quantity, IEnumerable<OrderAdditionalInput> selected, string? notes)
    {
        Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; OrderId = orderId; ProductId = productId; VariationId = variationId;
        ProductName = Required(productName, 150); VariationName = Optional(variationName, 100); UnitPrice = unitPrice; Quantity = quantity; Notes = Optional(notes, 500);
        foreach (var value in selected)
        {
            if (value.AdditionalId == Guid.Empty) throw new DomainException("Additional is required.");
            additionals.Add(new OrderItemAdditional(tenantId, establishmentId, Id, value.AdditionalId, value.Name, value.UnitPrice, value.Quantity));
        }
        BasePrice = unitPrice;
        Total = new Money((unitPrice.Amount + additionals.Sum(x => x.UnitPrice.Amount * x.Quantity.Value)) * quantity.Value);
    }

    internal OrderItem(Guid tenantId, Guid establishmentId, Guid orderId, Guid productId, Guid? variationId,
        string productName, string? variationName, ModifierCompositionPrice composition, Quantity quantity, string? notes)
    {
        Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; OrderId = orderId; ProductId = productId; VariationId = variationId;
        ProductName = Required(productName, 150); VariationName = Optional(variationName, 100);
        BasePrice = composition.BasePrice; UnitPrice = composition.UnitPrice; Quantity = quantity; Notes = Optional(notes, 500);
        foreach (var group in composition.Groups)
        {
            modifierGroups.Add(new OrderItemModifierGroup(tenantId, establishmentId, Id, group));
            foreach (var option in group.Options)
                additionals.Add(new OrderItemAdditional(tenantId, establishmentId, Id, option.OptionId, option.Name, option.UnitPrice, option.Quantity));
        }
        Total = new Money(UnitPrice.Amount * quantity.Value);
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? VariationId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string? VariationName { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money BasePrice { get; private set; }
    public Quantity Quantity { get; private set; }
    public string? Notes { get; private set; }
    public Money Total { get; private set; }
    public IReadOnlyCollection<OrderItemAdditional> Additionals => additionals;
    public IReadOnlyCollection<OrderItemModifierGroup> ModifierGroups => modifierGroups;

    private static string Required(string value, int max)
    { var result = value.Trim(); if (result.Length is < 1 || result.Length > max) throw new DomainException("Order item snapshot is invalid."); return result; }

    private static string? Optional(string? value, int max)
    { if (string.IsNullOrWhiteSpace(value)) return null; var result = value.Trim(); if (result.Length > max) throw new DomainException("Order item snapshot is invalid."); return result; }
}

public sealed class OrderItemModifierGroup : IEstablishmentScopedEntity
{
    private readonly List<OrderItemModifierOption> options = [];
    private OrderItemModifierGroup() { }
    internal OrderItemModifierGroup(Guid tenantId, Guid establishmentId, Guid orderItemId, ModifierGroupSnapshot snapshot)
    {
        Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; OrderItemId = orderItemId;
        ModifierGroupId = snapshot.GroupId; Name = snapshot.Name; PricingStrategy = snapshot.PricingStrategy;
        Price = snapshot.Price;
        foreach (var option in snapshot.Options) options.Add(new OrderItemModifierOption(tenantId, establishmentId, Id, option));
    }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid ModifierGroupId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ModifierPricingStrategy PricingStrategy { get; private set; }
    public Money Price { get; private set; }
    public IReadOnlyCollection<OrderItemModifierOption> Options => options;
}

public sealed class OrderItemModifierOption : IEstablishmentScopedEntity
{
    private OrderItemModifierOption() { }
    internal OrderItemModifierOption(Guid tenantId, Guid establishmentId, Guid groupId, ModifierOptionSnapshot snapshot)
    {
        Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; OrderItemModifierGroupId = groupId;
        ModifierOptionId = snapshot.OptionId; Name = snapshot.Name; UnitPrice = snapshot.UnitPrice; Quantity = snapshot.Quantity;
        PortionNumerator = snapshot.Portion?.Numerator; PortionDenominator = snapshot.Portion?.Denominator;
    }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid OrderItemModifierGroupId { get; private set; }
    public Guid ModifierOptionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Money UnitPrice { get; private set; }
    public Quantity Quantity { get; private set; }
    public int? PortionNumerator { get; private set; }
    public int? PortionDenominator { get; private set; }
}

public sealed class OrderItemAdditional : IEstablishmentScopedEntity
{
    private OrderItemAdditional()
    { }

    internal OrderItemAdditional(Guid tenantId, Guid establishmentId, Guid orderItemId, Guid additionalId, string name, Money unitPrice, Quantity quantity)
    { Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; OrderItemId = orderItemId; AdditionalId = additionalId; Name = name.Trim(); if (Name.Length is < 1 or > 150) throw new DomainException("Additional snapshot is invalid."); UnitPrice = unitPrice; Quantity = quantity; }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid OrderItemId { get; private set; }
    public Guid AdditionalId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Money UnitPrice { get; private set; }
    public Quantity Quantity { get; private set; }
}

public sealed class OrderStatusHistory : IEstablishmentScopedEntity
{
    private OrderStatusHistory()
    { }

    internal OrderStatusHistory(Guid tenantId, Guid establishmentId, Guid orderId, OrderStatus previousStatus, OrderStatus newStatus, DateTimeOffset occurredAt, Guid? actorId, string? note)
    { Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; OrderId = orderId; PreviousStatus = previousStatus; NewStatus = newStatus; OccurredAt = occurredAt; ActorId = actorId; Note = note; }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid OrderId { get; private set; }
    public OrderStatus PreviousStatus { get; private set; }
    public OrderStatus NewStatus { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid? ActorId { get; private set; }
    public string? Note { get; private set; }
}
