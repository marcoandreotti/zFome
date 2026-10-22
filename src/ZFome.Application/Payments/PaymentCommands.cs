using ZFome.Application.Abstractions.Commands;

namespace ZFome.Application.Payments;

public sealed record UpsertPaymentMethodCommand(Guid EstablishmentId, Guid? Id, string Code, string Name, bool IsOnline, bool AllowsChange) : ICommand<Guid>;
public sealed record SetPaymentMethodActiveCommand(Guid EstablishmentId, Guid PaymentMethodId, bool IsActive) : ICommand;
public sealed record CreatePaymentCommand(Guid EstablishmentId, Guid OrderId, Guid PaymentMethodId, decimal Amount, decimal? ReceivedAmount) : ICommand<Guid>;
public sealed record ConfirmPaymentCommand(Guid EstablishmentId, Guid PaymentId, decimal Amount, string IdempotencyKey, string? ExternalId) : ICommand<Guid>;
public sealed record FailPaymentCommand(Guid EstablishmentId, Guid PaymentId) : ICommand;
public sealed record CancelPaymentCommand(Guid EstablishmentId, Guid PaymentId) : ICommand;
