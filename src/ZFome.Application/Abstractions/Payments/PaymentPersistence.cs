using ZFome.Domain.Payments;

namespace ZFome.Application.Abstractions.Payments;

public interface IPaymentMethodRepository
{
    Task<PaymentMethod?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(Guid tenantId, Guid establishmentId, string code, Guid? exceptId, CancellationToken cancellationToken);
    Task AddAsync(PaymentMethod method, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
public interface IPaymentRepository
{
    Task<Payment?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken);
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
public interface IPaymentIdempotencyRepository
{
    Task<PaymentIdempotency?> FindAsync(Guid tenantId, Guid establishmentId, string key, CancellationToken cancellationToken);
    Task AddAsync(PaymentIdempotency idempotency, CancellationToken cancellationToken);
}
public interface IPaymentOrderGateway
{
    Task<PaymentOrderSnapshot?> GetAsync(Guid tenantId, Guid establishmentId, Guid orderId, CancellationToken cancellationToken);
    Task<decimal> GetConfirmedAmountAsync(Guid tenantId, Guid establishmentId, Guid orderId, CancellationToken cancellationToken);
}
public interface IPaymentConfirmationTransaction
{
    Task<T> ExecuteForOrderAsync<T>(Guid tenantId, Guid establishmentId, Guid orderId, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
public interface IPaymentReadGateway
{
    Task<IReadOnlyList<PaymentMethodReadModel>> ListMethodsAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken);
    Task<OrderPaymentsReadModel> GetOrderPaymentsAsync(Guid tenantId, Guid establishmentId, Guid orderId, CancellationToken cancellationToken);
    Task<PaymentMethodSearchResult> SearchMethodsAsync(Guid tenantId, Guid establishmentId, string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken);
}
public sealed record PaymentOrderSnapshot(Guid OrderId, decimal Total, string OperationalStatus);
public sealed record PaymentMethodReadModel(Guid Id, string Code, string Name, bool IsOnline, bool AllowsChange, bool IsActive);
public sealed record PaymentMethodSearchResult(int Total, IReadOnlyList<PaymentMethodReadModel> Items);
public sealed record PaymentReadModel(Guid Id, string MethodCode, string MethodName, decimal Amount, decimal? ReceivedAmount, decimal Change, PaymentStatus Status, string? ExternalId, DateTimeOffset CreatedAt, DateTimeOffset? ConfirmedAt);
public sealed record OrderPaymentsReadModel(Guid OrderId, decimal DueAmount, decimal ConfirmedAmount, bool IsFullyCovered, string OperationalStatus, IReadOnlyList<PaymentReadModel> Payments);
