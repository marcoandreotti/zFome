namespace ZFome.Contracts.Administration;

public sealed record PagedResponse<T>(int Page, int PageSize, int Total, IReadOnlyList<T> Items);

public sealed record CustomerUpsertRequest(string Name, string Phone, string? Email);
public sealed record CustomerAddressUpsertRequest(string Label, string Street, string Number, string? Complement, string Neighborhood, string City, string State, string PostalCode, bool IsPrimary);
public sealed record CustomerResponse(Guid Id, string Name, string Phone, string? Email, IReadOnlyList<CustomerAddressResponse> Addresses);
public sealed record CustomerAddressResponse(Guid Id, string Label, string Street, string Number, string? Complement, string Neighborhood, string City, string State, string PostalCode, bool IsPrimary);

public sealed record OrderSummaryItemResponse(decimal Quantity, string ProductName, string? VariationName);
public sealed record OrderSummaryResponse(Guid Id, long Number, string ServiceType, string Status, string? CustomerName, string? CustomerPhone, decimal Total, DateTimeOffset CreatedAt, DateTimeOffset? ScheduledAtUtc = null, string? ScheduledTimeZoneId = null, IReadOnlyList<OrderSummaryItemResponse>? Items = null, bool PreparationAvailable = true);
public sealed record OrderTransitionRequest(string? Note);
public sealed record OrderDetailResponse(Guid Id, long? Number, string? PublicReference, string ServiceType, string Status, string? CustomerName, string? CustomerPhone, string? TableCode, decimal Subtotal, decimal Discount, decimal Fees, decimal Total, string? CouponCode, decimal ConfirmedAmount, bool IsFullyPaid, IReadOnlyList<AdminOrderItemResponse> Items, IReadOnlyList<AdminOrderHistoryResponse> History,
    AdminDeliveryAddressResponse? DeliveryAddress = null, Guid? DeliveryRegionId = null, string? DeliveryRegionName = null, decimal DeliveryFee = 0, int? DeliveryEstimatedMinutes = null, DateTimeOffset? ScheduledAtUtc = null, string? ScheduledTimeZoneId = null, bool PreparationAvailable = true);
public sealed record OrderSchedulingPolicyResponse(string ServiceType, bool IsEnabled, int SlotIntervalMinutes, int MinimumAdvanceMinutes, int HorizonDays, int? MaximumOrdersPerSlot);
public sealed record OrderSchedulingConfigurationResponse(string TimeZoneId, IReadOnlyList<OrderSchedulingPolicyResponse> Policies);
public sealed record OrderSchedulingPolicyRequest(bool IsEnabled, int MinimumAdvanceMinutes, int HorizonDays, int? MaximumOrdersPerSlot);
public sealed record AdminDeliveryAddressResponse(string Street, string Number, string? Complement, string Neighborhood, string City, string State, string PostalCode);
public sealed record AdminOrderItemResponse(Guid Id, string ProductName, string? VariationName, decimal UnitPrice, decimal Quantity, decimal Total, string? Notes, IReadOnlyList<AdminOrderAdditionalResponse> Additionals, decimal BasePrice = 0, IReadOnlyList<AdminOrderModifierGroupResponse>? ModifierGroups = null);
public sealed record AdminOrderAdditionalResponse(string Name, decimal UnitPrice, decimal Quantity);
public sealed record AdminOrderModifierGroupResponse(Guid GroupId, string Name, string PricingStrategy, decimal Price, IReadOnlyList<AdminOrderModifierOptionResponse> Options);
public sealed record AdminOrderModifierOptionResponse(Guid OptionId, string Name, decimal UnitPrice, decimal Quantity, int? PortionNumerator, int? PortionDenominator);
public sealed record AdminOrderHistoryResponse(string PreviousStatus, string NewStatus, DateTimeOffset OccurredAt, Guid? ActorId, string? Note);
public sealed record KitchenTicketResponse(Guid Id, long Number, string ServiceType, string Status, string? CustomerName, string? TableCode, DateTimeOffset ConfirmedAt, DateTimeOffset? PreparationStartedAt, string Action, IReadOnlyList<KitchenItemResponse> Items);
public sealed record KitchenItemResponse(Guid Id, string ProductName, string? VariationName, decimal Quantity, string? Notes, IReadOnlyList<KitchenAdditionalResponse> Additionals);
public sealed record KitchenAdditionalResponse(string Name, decimal Quantity);

public sealed record CouponUpsertRequest(string Code, string? Description, string DiscountType, decimal Value, decimal MinimumOrder, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int? MaximumUses);
public sealed record CouponResponse(Guid Id, string Code, string? Description, string DiscountType, decimal Value, decimal MinimumOrder, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int? MaximumUses, int UsedCount, bool IsActive);
public sealed record SetActiveRequest(bool IsActive);

public sealed record PaymentMethodUpsertRequest(string Code, string Name, bool IsOnline, bool AllowsChange);
public sealed record PaymentMethodResponse(Guid Id, string Code, string Name, bool IsOnline, bool AllowsChange, bool IsActive);
public sealed record PaymentCreateRequest(Guid PaymentMethodId, decimal Amount, decimal? ReceivedAmount);
public sealed record PaymentConfirmRequest(decimal Amount, string IdempotencyKey, string? ExternalId);
public sealed record PaymentResponse(Guid Id, string MethodCode, string MethodName, decimal Amount, decimal? ReceivedAmount, decimal Change, string Status, string? ExternalId, DateTimeOffset CreatedAt, DateTimeOffset? ConfirmedAt);
public sealed record OrderPaymentsResponse(Guid OrderId, decimal DueAmount, decimal ConfirmedAmount, bool IsFullyCovered, string OperationalStatus, IReadOnlyList<PaymentResponse> Payments);
