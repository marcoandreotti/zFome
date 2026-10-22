namespace ZFome.Contracts.PublicOrdering;

public sealed record PublicContextResponse(
    string EstablishmentName,
    string Slug,
    PublicThemeResponse Theme,
    PublicTableResponse? Table,
    IReadOnlyList<PublicPaymentMethodResponse> PaymentMethods,
    IReadOnlyList<PublicServiceAvailabilityResponse> Availability);

public sealed record PublicThemeResponse(string PrimaryColor, string SecondaryColor, string BackgroundColor, string TextColor, string FontFamily, string? LogoUrl);
public sealed record PublicTableResponse(string Code, string Token);
public sealed record PublicPaymentMethodResponse(Guid Id, string Code, string Name, bool IsOnline, bool AllowsChange);
public sealed record PublicServiceAvailabilityResponse(string ServiceType, bool IsAvailable, string Reason, string? Message, DateTimeOffset? NextOpening);

public sealed record PublicCustomerRequest(string Name, string Phone, string? Email, PublicAddressRequest? Address);
public sealed record PublicAddressRequest(string Label, string Street, string Number, string? Complement, string Neighborhood, string City, string State, string PostalCode);
public sealed record PublicCustomerResponse(Guid CustomerId, Guid? AddressId);

public sealed record PublicOrderItemRequest(Guid ProductId, Guid? VariationId, decimal Quantity, string? Notes, IReadOnlyList<PublicOrderAdditionalRequest> Additionals);
public sealed record PublicOrderAdditionalRequest(Guid AdditionalId, decimal Quantity, Guid? GroupId = null, int? PortionNumerator = null, int? PortionDenominator = null);
public sealed record PublicOrderSimulationRequest(string ServiceType, Guid? CustomerId, Guid? CustomerAddressId, string? TableToken, PublicAddressRequest? DeliveryAddress, string? CouponCode, Guid? PaymentMethodId, IReadOnlyList<PublicOrderItemRequest> Items, DateTimeOffset? ScheduledAtUtc = null);
public sealed record PublicOrderSimulationResponse(decimal Subtotal, decimal Discount, decimal Fees, decimal Total, string? CouponCode, IReadOnlyList<PublicOrderItemResponse> Items,
    Guid? DeliveryRegionId = null, string? DeliveryRegionName = null, decimal? DeliveryFee = null, int? DeliveryEstimatedMinutes = null, DateTimeOffset? DeliveryQuoteIssuedAt = null, DateTimeOffset? ScheduledAtUtc = null, string? ScheduledTimeZoneId = null);
public sealed record PublicOrderItemResponse(string ProductName, string? VariationName, decimal UnitPrice, decimal Quantity, decimal Total, IReadOnlyList<PublicOrderAdditionalResponse> Additionals, decimal BasePrice = 0, IReadOnlyList<PublicOrderModifierGroupResponse>? ModifierGroups = null);
public sealed record PublicOrderAdditionalResponse(string Name, decimal UnitPrice, decimal Quantity);
public sealed record PublicOrderModifierGroupResponse(Guid GroupId, string Name, string PricingStrategy, decimal Price, IReadOnlyList<PublicOrderModifierOptionResponse> Options);
public sealed record PublicOrderModifierOptionResponse(Guid OptionId, string Name, decimal UnitPrice, decimal Quantity, int? PortionNumerator, int? PortionDenominator);

public sealed record PublicOrderConfirmationRequest(string ServiceType, Guid? CustomerId, Guid? CustomerAddressId, string? TableToken, PublicAddressRequest? DeliveryAddress, string? CouponCode, Guid PaymentMethodId, decimal? ReceivedAmount, IReadOnlyList<PublicOrderItemRequest> Items,
    Guid? DeliveryRegionId = null, decimal? ExpectedDeliveryFee = null, int? ExpectedDeliveryEstimatedMinutes = null, DateTimeOffset? DeliveryQuoteIssuedAt = null, DateTimeOffset? ScheduledAtUtc = null);
public sealed record PublicOrderConfirmationResponse(string Reference, long Number, string Status, decimal Total);
public sealed record PublicOrderTrackingResponse(string Reference, long Number, string ServiceType, string Status, decimal Subtotal, decimal Discount, decimal Fees, decimal Total, string? CouponCode, IReadOnlyList<PublicOrderItemResponse> Items, IReadOnlyList<PublicOrderHistoryResponse> History, string? DeliveryRegionName = null, decimal DeliveryFee = 0, int? DeliveryEstimatedMinutes = null, DateTimeOffset? ScheduledAtUtc = null, string? ScheduledTimeZoneId = null);
public sealed record PublicOrderHistoryResponse(string Status, DateTimeOffset OccurredAt, string? Note);
public sealed record PublicOrderCancellationRequest(string? Reason);
public sealed record PublicOrderSchedulingSlotsResponse(string ServiceType, bool IsEnabled, string TimeZoneId, int SlotIntervalMinutes, int MinimumAdvanceMinutes, int HorizonDays, IReadOnlyList<PublicOrderSchedulingSlotResponse> Slots);
public sealed record PublicOrderSchedulingSlotResponse(DateTimeOffset StartsAt, int? RemainingCapacity);
