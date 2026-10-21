using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Payments;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Exceptions;
using ZFome.Application.Tenancy;
using ZFome.Domain.Payments;
using ZFome.Domain.SharedKernel;

namespace ZFome.Application.Payments;

public sealed class UpsertPaymentMethodCommandHandler(EstablishmentScopeResolver scopeResolver, IPaymentMethodRepository repository, TimeProvider timeProvider) : ICommandHandler<UpsertPaymentMethodCommand, Guid>
{
    public async Task<Guid> HandleAsync(UpsertPaymentMethodCommand command, CancellationToken cancellationToken)
    { var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken); var code = PaymentMethod.NormalizeCode(command.Code); if (await repository.CodeExistsAsync(scope.TenantId, scope.EstablishmentId, code, command.Id, cancellationToken)) throw new ConflictException("Payment-method code already exists."); PaymentMethod method; var now = timeProvider.GetUtcNow(); if (command.Id is { } id) { method = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, id, cancellationToken) ?? throw new NotFoundException("Payment method was not found."); method.Update(command.Code, command.Name, command.IsOnline, command.AllowsChange, now); await repository.SaveChangesAsync(cancellationToken); } else { method = PaymentMethod.Create(scope.TenantId, scope.EstablishmentId, command.Code, command.Name, command.IsOnline, command.AllowsChange, now); await repository.AddAsync(method, cancellationToken); } return method.Id; }
}
public sealed class SetPaymentMethodActiveCommandHandler(EstablishmentScopeResolver scopeResolver, IPaymentMethodRepository repository, TimeProvider timeProvider) : ICommandHandler<SetPaymentMethodActiveCommand>
{ public async Task HandleAsync(SetPaymentMethodActiveCommand command, CancellationToken cancellationToken) { var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken); var method = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, command.PaymentMethodId, cancellationToken) ?? throw new NotFoundException("Payment method was not found."); if (command.IsActive) method.Activate(timeProvider.GetUtcNow()); else method.Deactivate(timeProvider.GetUtcNow()); await repository.SaveChangesAsync(cancellationToken); } }
public sealed class CreatePaymentCommandHandler(EstablishmentScopeResolver scopeResolver, IPaymentMethodRepository methods, IPaymentRepository payments, IPaymentOrderGateway orders, TimeProvider timeProvider) : ICommandHandler<CreatePaymentCommand, Guid>
{
    public async Task<Guid> HandleAsync(CreatePaymentCommand command, CancellationToken cancellationToken)
    { var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken); _ = await orders.GetAsync(scope.TenantId, scope.EstablishmentId, command.OrderId, cancellationToken) ?? throw new NotFoundException("Order was not found."); var method = await methods.GetAsync(scope.TenantId, scope.EstablishmentId, command.PaymentMethodId, cancellationToken) ?? throw new NotFoundException("Payment method was not found."); var payment = Payment.Create(scope.TenantId, scope.EstablishmentId, command.OrderId, method, new Money(command.Amount), command.ReceivedAmount is null ? null : new Money(command.ReceivedAmount.Value), timeProvider.GetUtcNow()); await payments.AddAsync(payment, cancellationToken); return payment.Id; }
}
public sealed class ConfirmPaymentCommandHandler(EstablishmentScopeResolver scopeResolver, IPaymentRepository payments, IPaymentIdempotencyRepository idempotency, IPaymentOrderGateway orders, IPaymentConfirmationTransaction transaction, TimeProvider timeProvider) : ICommandHandler<ConfirmPaymentCommand, Guid>
{
    public async Task<Guid> HandleAsync(ConfirmPaymentCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken); var hash = Hash(command);
        var replay = await idempotency.FindAsync(scope.TenantId, scope.EstablishmentId, command.IdempotencyKey.Trim(), cancellationToken); if (replay is not null) return ResolveReplay(replay, hash);
        var initial = await payments.GetAsync(scope.TenantId, scope.EstablishmentId, command.PaymentId, cancellationToken) ?? throw new NotFoundException("Payment was not found.");
        return await transaction.ExecuteForOrderAsync(scope.TenantId, scope.EstablishmentId, initial.OrderId, async token =>
        {
            var concurrentReplay = await idempotency.FindAsync(scope.TenantId, scope.EstablishmentId, command.IdempotencyKey.Trim(), token); if (concurrentReplay is not null) return ResolveReplay(concurrentReplay, hash);
            var payment = await payments.GetAsync(scope.TenantId, scope.EstablishmentId, command.PaymentId, token) ?? throw new NotFoundException("Payment was not found.");
            var order = await orders.GetAsync(scope.TenantId, scope.EstablishmentId, payment.OrderId, token) ?? throw new NotFoundException("Order was not found.");
            if (payment.Amount.Amount != decimal.Round(command.Amount, 2, MidpointRounding.AwayFromZero)) throw new ConflictException("Payment confirmation payload differs from the payment.");
            var covered = await orders.GetConfirmedAmountAsync(scope.TenantId, scope.EstablishmentId, payment.OrderId, token); if (covered + payment.Amount.Amount > order.Total) throw new ConflictException("Confirmed payments would exceed the authoritative order total.");
            payment.Confirm(command.ExternalId, timeProvider.GetUtcNow()); await idempotency.AddAsync(PaymentIdempotency.Create(scope.TenantId, scope.EstablishmentId, command.IdempotencyKey.Trim(), hash, payment.Id, timeProvider.GetUtcNow()), token); await payments.SaveChangesAsync(token); return payment.Id;
        }, cancellationToken);
    }
    private static Guid ResolveReplay(PaymentIdempotency replay, string hash) { if (replay.PayloadHash != hash) throw new ConflictException("Idempotency key was already used with different payment data."); return replay.PaymentId; }
    private static string Hash(ConfirmPaymentCommand command) { var payload = $"{command.PaymentId:N}|{command.Amount.ToString("0.00", CultureInfo.InvariantCulture)}|{command.ExternalId?.Trim()}"; return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant(); }
}
public sealed class FailPaymentCommandHandler(EstablishmentScopeResolver scopeResolver, IPaymentRepository repository, TimeProvider timeProvider) : ICommandHandler<FailPaymentCommand>
{ public async Task HandleAsync(FailPaymentCommand command, CancellationToken cancellationToken) { var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken); var payment = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, command.PaymentId, cancellationToken) ?? throw new NotFoundException("Payment was not found."); payment.Fail(timeProvider.GetUtcNow()); await repository.SaveChangesAsync(cancellationToken); } }
public sealed class CancelPaymentCommandHandler(EstablishmentScopeResolver scopeResolver, IPaymentRepository repository, TimeProvider timeProvider) : ICommandHandler<CancelPaymentCommand>
{ public async Task HandleAsync(CancelPaymentCommand command, CancellationToken cancellationToken) { var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken); var payment = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, command.PaymentId, cancellationToken) ?? throw new NotFoundException("Payment was not found."); payment.Cancel(timeProvider.GetUtcNow()); await repository.SaveChangesAsync(cancellationToken); } }
public sealed class ListPaymentMethodsQueryHandler(EstablishmentScopeResolver scopeResolver, IPaymentReadGateway gateway) : IQueryHandler<ListPaymentMethodsQuery, IReadOnlyList<PaymentMethodReadModel>>
{ public async Task<IReadOnlyList<PaymentMethodReadModel>> HandleAsync(ListPaymentMethodsQuery query, CancellationToken cancellationToken) { var scope = await scopeResolver.ResolveAsync(query.EstablishmentId, cancellationToken); return await gateway.ListMethodsAsync(scope.TenantId, scope.EstablishmentId, cancellationToken); } }
public sealed class GetOrderPaymentsQueryHandler(EstablishmentScopeResolver scopeResolver, IPaymentReadGateway gateway) : IQueryHandler<GetOrderPaymentsQuery, OrderPaymentsReadModel>
{ public async Task<OrderPaymentsReadModel> HandleAsync(GetOrderPaymentsQuery query, CancellationToken cancellationToken) { var scope = await scopeResolver.ResolveAsync(query.EstablishmentId, cancellationToken); return await gateway.GetOrderPaymentsAsync(scope.TenantId, scope.EstablishmentId, query.OrderId, cancellationToken); } }
public sealed class SearchPaymentMethodsQueryHandler(EstablishmentScopeResolver scopeResolver,IPaymentReadGateway gateway):IQueryHandler<SearchPaymentMethodsQuery,PaymentMethodSearchResult>
{ public async Task<PaymentMethodSearchResult> HandleAsync(SearchPaymentMethodsQuery query,CancellationToken cancellationToken){var scope=await scopeResolver.ResolveAsync(query.EstablishmentId,cancellationToken);return await gateway.SearchMethodsAsync(scope.TenantId,scope.EstablishmentId,query.Search,query.IsActive,query.Page,query.PageSize,cancellationToken);} }
