using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ZFome.Application.Abstractions.Payments;

namespace ZFome.Infrastructure.Persistence.Write;

/// <summary>
/// Implementação do gateway de acesso a dados para operações relacionadas a pedidos de pagamento.
/// </summary>
public sealed class PaymentOrderGateway(ZFomeDbContext context) : IPaymentOrderGateway
{
    public async Task<PaymentOrderSnapshot?> GetAsync(Guid tenantId, Guid establishmentId, Guid orderId, CancellationToken cancellationToken)
    {
        const string sql = "select id as OrderId, total, status as OperationalStatus from orders.\"order\" where tenant_id=@TenantId and establishment_id=@EstablishmentId and id=@OrderId;";
        var connection = context.Database.GetDbConnection(); if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PaymentOrderSnapshot>(new CommandDefinition(sql, new { TenantId = tenantId, EstablishmentId = establishmentId, OrderId = orderId }, context.Database.CurrentTransaction?.GetDbTransaction(), cancellationToken: cancellationToken));
    }

    public async Task<decimal> GetConfirmedAmountAsync(Guid tenantId, Guid establishmentId, Guid orderId, CancellationToken cancellationToken)
    {
        const string sql = "select coalesce(sum(amount), 0) from payments.payment where tenant_id=@TenantId and establishment_id=@EstablishmentId and order_id=@OrderId and status='Confirmed';";
        var connection = context.Database.GetDbConnection(); if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(sql, new { TenantId = tenantId, EstablishmentId = establishmentId, OrderId = orderId }, context.Database.CurrentTransaction?.GetDbTransaction(), cancellationToken: cancellationToken));
    }
}

public sealed class PaymentConfirmationTransaction(ZFomeDbContext context) : IPaymentConfirmationTransaction
{
    public async Task<T> ExecuteForOrderAsync<T>(Guid tenantId, Guid establishmentId, Guid orderId, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        const string sql = "select id from orders.\"order\" where tenant_id=@TenantId and establishment_id=@EstablishmentId and id=@OrderId for update;";
        var locked = await context.Database.GetDbConnection().ExecuteScalarAsync<Guid?>(new CommandDefinition(sql, new { TenantId = tenantId, EstablishmentId = establishmentId, OrderId = orderId }, transaction.GetDbTransaction(), cancellationToken: cancellationToken));
        if (locked is null) throw new InvalidOperationException("Order could not be locked for payment confirmation.");
        var result = await operation(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
    }
}