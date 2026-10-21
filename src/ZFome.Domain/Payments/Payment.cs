using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Payments;

public enum PaymentStatus
{ Pending, Confirmed, Failed, Cancelled }

/// <summary>
/// Representa um método de pagamento no escopo de um estabelecimento, com regras de validade e elegibilidade.
/// </summary>
public sealed class PaymentMethod : IEstablishmentScopedEntity
{
    private PaymentMethod()
    { }

    private PaymentMethod(Guid tenantId, Guid establishmentId, string code, string name, bool isOnline, bool allowsChange, DateTimeOffset now)
    { if (tenantId == Guid.Empty || establishmentId == Guid.Empty) throw new DomainException("Payment-method scope is required."); Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; CreatedAt = now; Update(code, name, isOnline, allowsChange, now); IsActive = true; }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsOnline { get; private set; }
    public bool AllowsChange { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static PaymentMethod Create(Guid tenantId, Guid establishmentId, string code, string name, bool isOnline, bool allowsChange, DateTimeOffset now) => new(tenantId, establishmentId, code, name, isOnline, allowsChange, now);

    public void Update(string code, string name, bool isOnline, bool allowsChange, DateTimeOffset now)
    { Code = NormalizeCode(code); Name = Required(name, 100); IsOnline = isOnline; AllowsChange = allowsChange; UpdatedAt = now; }

    public void Activate(DateTimeOffset now)
    { IsActive = true; UpdatedAt = now; }

    public void Deactivate(DateTimeOffset now)
    { IsActive = false; UpdatedAt = now; }

    public static string NormalizeCode(string code)
    { var value = new string(code.Trim().Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant(); if (value.Length is < 2 or > 30) throw new DomainException("Payment-method code is invalid."); return value; }

    private static string Required(string value, int max)
    { var result = value.Trim(); if (result.Length is < 1 || result.Length > max) throw new DomainException("Payment-method name is invalid."); return result; }
}

public sealed class Payment : IEstablishmentScopedEntity
{
    private Payment()
    { }

    private Payment(Guid tenantId, Guid establishmentId, Guid orderId, PaymentMethod method, Money amount, Money? receivedAmount, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty || orderId == Guid.Empty) throw new DomainException("Payment scope and order are required.");
        if (method.TenantId != tenantId || method.EstablishmentId != establishmentId || !method.IsActive) throw new DomainException("Active payment method from the same establishment is required.");
        if (amount.Amount <= 0) throw new DomainException("Payment amount must be positive.");
        if (receivedAmount is not null && (!method.AllowsChange || receivedAmount.Value.Amount < amount.Amount)) throw new DomainException("Received amount is invalid for this payment method.");
        Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; OrderId = orderId; PaymentMethodId = method.Id; PaymentMethodCode = method.Code; PaymentMethodName = method.Name; IsOnline = method.IsOnline;
        Amount = amount; ReceivedAmount = receivedAmount; Change = receivedAmount is null ? Money.Zero : new Money(receivedAmount.Value.Amount - amount.Amount); Status = PaymentStatus.Pending; CreatedAt = now; UpdatedAt = now; Version = Guid.NewGuid();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid PaymentMethodId { get; private set; }
    public string PaymentMethodCode { get; private set; } = string.Empty;
    public string PaymentMethodName { get; private set; } = string.Empty;
    public bool IsOnline { get; private set; }
    public Money Amount { get; private set; }
    public Money? ReceivedAmount { get; private set; }
    public Money Change { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? ExternalId { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Payment Create(Guid tenantId, Guid establishmentId, Guid orderId, PaymentMethod method, Money amount, Money? receivedAmount, DateTimeOffset now) => new(tenantId, establishmentId, orderId, method, amount, receivedAmount, now);

    public void Confirm(string? externalId, DateTimeOffset now)
    { EnsurePending(); ExternalId = NormalizeExternalId(externalId); Status = PaymentStatus.Confirmed; ConfirmedAt = now; Touch(now); }

    public void Fail(DateTimeOffset now)
    { EnsurePending(); Status = PaymentStatus.Failed; FailedAt = now; Touch(now); }

    public void Cancel(DateTimeOffset now)
    { if (Status is PaymentStatus.Cancelled or PaymentStatus.Failed) throw new DomainException("Payment cannot be cancelled from its current state."); Status = PaymentStatus.Cancelled; CancelledAt = now; Touch(now); }

    private void EnsurePending()
    { if (Status != PaymentStatus.Pending) throw new DomainException("Payment is not pending."); }

    private void Touch(DateTimeOffset now)
    { UpdatedAt = now; Version = Guid.NewGuid(); }

    private static string? NormalizeExternalId(string? value)
    { if (string.IsNullOrWhiteSpace(value)) return null; var result = value.Trim(); if (result.Length > 100) throw new DomainException("External payment id is invalid."); return result; }
}

public sealed class PaymentIdempotency : IEstablishmentScopedEntity
{
    private PaymentIdempotency()
    { }

    private PaymentIdempotency(Guid tenantId, Guid establishmentId, string key, string payloadHash, Guid paymentId, DateTimeOffset createdAt)
    { Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; Key = key; PayloadHash = payloadHash; PaymentId = paymentId; CreatedAt = createdAt; }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public Guid PaymentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static PaymentIdempotency Create(Guid tenantId, Guid establishmentId, string key, string payloadHash, Guid paymentId, DateTimeOffset now)
    { var normalized = key.Trim(); if (tenantId == Guid.Empty || establishmentId == Guid.Empty || normalized.Length is < 8 or > 100 || payloadHash.Length != 64 || paymentId == Guid.Empty) throw new DomainException("Payment idempotency data is invalid."); return new(tenantId, establishmentId, normalized, payloadHash, paymentId, now); }
}