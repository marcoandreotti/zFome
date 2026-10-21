using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ZFome.Application.Abstractions.Ordering;

namespace ZFome.Infrastructure.Persistence.Write;

/// <summary>
/// Implementação do gerador de sequência de números de pedidos, garantindo a reserva de um número único para cada pedido dentro do contexto de um locatário (tenant) e estabelecimento específico.
/// </summary>
public sealed class OrderNumberSequence(ZFomeDbContext context) : IOrderNumberSequence
{
    public async Task<long> ReserveAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken)
    {
        const string sql = """
            insert into orders.order_number_counter (tenant_id, establishment_id, last_number)
            values (@TenantId, @EstablishmentId, 1)
            on conflict (tenant_id, establishment_id)
            do update set last_number = orders.order_number_counter.last_number + 1
            returning last_number;
            """;
        var connection = context.Database.GetDbConnection();
        var transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { TenantId = tenantId, EstablishmentId = establishmentId }, transaction, cancellationToken: cancellationToken));
    }
}

public sealed class OrderConfirmationTransaction(ZFomeDbContext context) : IOrderConfirmationTransaction
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is not null) { await operation(cancellationToken); return; }
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}