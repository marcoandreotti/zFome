using ZFome.Application.Abstractions.Payments;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Exceptions;
using ZFome.Application.Payments;
using ZFome.Application.Tenancy;
using ZFome.Domain.Payments;

namespace ZFome.Application.Tests.Payments;

public sealed class PaymentApplicationTests
{
    private static readonly Guid TenantId = Guid.NewGuid(); private static readonly Guid UserId = Guid.NewGuid(); private static readonly Guid EstablishmentId = Guid.NewGuid(); private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
    [Fact] public async Task Upsert_method_uses_authenticated_scope_and_normalized_code()
    { var methods = new Methods(); var id = await new UpsertPaymentMethodCommandHandler(Resolver(), methods, new Clock()).HandleAsync(new(EstablishmentId, null, " pix-1 ", "Pix", true, false), CancellationToken.None); Assert.Equal(id, methods.Value!.Id); Assert.Equal("PIX1", methods.Value.Code); Assert.Equal(TenantId, methods.Value.TenantId); }
    [Fact] public async Task Create_payment_preserves_method_snapshot()
    { var method = Method(); var payments = new Payments(); var id = await new CreatePaymentCommandHandler(Resolver(), new Methods(method), payments, new Orders(), new Clock()).HandleAsync(new(EstablishmentId, OrderId, method.Id, 30, null), CancellationToken.None); Assert.Equal(id, payments.Value!.Id); Assert.Equal(method.Code, payments.Value.PaymentMethodCode); }
    [Fact] public async Task Confirmation_is_idempotent_for_same_payload_and_conflicts_for_different_payload()
    {
        var method = Method(); var payment = Payment.Create(TenantId, EstablishmentId, OrderId, method, new(30), null, Now); var payments = new Payments(payment); var keys = new Keys(); var transaction = new Transaction(); var handler = new ConfirmPaymentCommandHandler(Resolver(), payments, keys, new Orders(100, 0), transaction, new Clock());
        var command = new ConfirmPaymentCommand(EstablishmentId, payment.Id, 30, "operation-123", "external-1"); var first = await handler.HandleAsync(command, CancellationToken.None); var replay = await handler.HandleAsync(command, CancellationToken.None);
        Assert.Equal(first, replay); Assert.Equal(1, keys.AddCount); Assert.Equal(PaymentStatus.Confirmed, payment.Status); Assert.Equal(1, transaction.Count);
        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(command with { Amount = 31 }, CancellationToken.None));
    }
    [Fact] public async Task Confirmation_uses_authoritative_total_and_ignores_operational_status()
    { var method = Method(); var payment = Payment.Create(TenantId, EstablishmentId, OrderId, method, new(20), null, Now); var orders = new Orders(50, 40, "Preparing"); var handler = new ConfirmPaymentCommandHandler(Resolver(), new Payments(payment), new Keys(), orders, new Transaction(), new Clock()); await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(new(EstablishmentId, payment.Id, 20, "operation-456", null), CancellationToken.None)); Assert.Equal("Preparing", orders.Status); Assert.Equal(PaymentStatus.Pending, payment.Status); }
    [Fact] public async Task Validators_reject_invalid_payment_data()
    { Assert.False((await new CreatePaymentCommandValidator().ValidateAsync(new CreatePaymentCommand(Guid.Empty, Guid.Empty, Guid.Empty, 0, -1))).IsValid); Assert.False((await new ConfirmPaymentCommandValidator().ValidateAsync(new ConfirmPaymentCommand(Guid.Empty, Guid.Empty, 0, "short", null))).IsValid); }
    private static PaymentMethod Method() => PaymentMethod.Create(TenantId, EstablishmentId, "PIX", "Pix", true, false, Now);
    private static EstablishmentScopeResolver Resolver() => new(new Context(), new Access());
    private sealed class Context : ITenantContext { public bool HasTenant => true; public Guid TenantId => PaymentApplicationTests.TenantId; public bool HasUser => true; public Guid UserId => PaymentApplicationTests.UserId; public Guid GetRequiredTenantId() => TenantId; public Guid GetRequiredUserId() => UserId; }
    private sealed class Access : IEstablishmentAccessGateway { public Task<bool> HasActiveAccessAsync(Guid tenantId, Guid userId, Guid establishmentId, CancellationToken cancellationToken) => Task.FromResult(tenantId == TenantId && establishmentId == EstablishmentId); }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Methods(PaymentMethod? value = null) : IPaymentMethodRepository { public PaymentMethod? Value { get; private set; } = value; public Task<PaymentMethod?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) => Task.FromResult(Value is { } x && x.Id == id ? x : null); public Task<bool> CodeExistsAsync(Guid tenantId, Guid establishmentId, string code, Guid? exceptId, CancellationToken cancellationToken) => Task.FromResult(false); public Task AddAsync(PaymentMethod method, CancellationToken cancellationToken) { Value = method; return Task.CompletedTask; } public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class Payments(Payment? value = null) : IPaymentRepository { public Payment? Value { get; private set; } = value; public Task<Payment?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) => Task.FromResult(Value is { } x && x.Id == id && x.TenantId == tenantId && x.EstablishmentId == establishmentId ? x : null); public Task AddAsync(Payment payment, CancellationToken cancellationToken) { Value = payment; return Task.CompletedTask; } public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class Keys : IPaymentIdempotencyRepository { private PaymentIdempotency? value; public int AddCount { get; private set; } public Task<PaymentIdempotency?> FindAsync(Guid tenantId, Guid establishmentId, string key, CancellationToken cancellationToken) => Task.FromResult(value is { } x && x.Key == key ? x : null); public Task AddAsync(PaymentIdempotency idempotency, CancellationToken cancellationToken) { value = idempotency; AddCount++; return Task.CompletedTask; } }
    private sealed class Orders(decimal total = 100, decimal covered = 0, string status = "Confirmed") : IPaymentOrderGateway { public string Status => status; public Task<PaymentOrderSnapshot?> GetAsync(Guid tenantId, Guid establishmentId, Guid orderId, CancellationToken cancellationToken) => Task.FromResult<PaymentOrderSnapshot?>(orderId == OrderId ? new(orderId, total, status) : null); public Task<decimal> GetConfirmedAmountAsync(Guid tenantId, Guid establishmentId, Guid orderId, CancellationToken cancellationToken) => Task.FromResult(covered); }
    private sealed class Transaction : IPaymentConfirmationTransaction { public int Count { get; private set; } public async Task<T> ExecuteForOrderAsync<T>(Guid tenantId, Guid establishmentId, Guid orderId, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) { Count++; return await operation(cancellationToken); } }
}
