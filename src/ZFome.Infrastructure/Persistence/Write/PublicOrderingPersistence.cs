using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZFome.Application.Abstractions.PublicOrdering;
using ZFome.Application.Exceptions;
using ZFome.Domain.Ordering;

namespace ZFome.Infrastructure.Persistence.Write;

/// <summary>
/// Representa um repositório de solicitações de pedidos públicos que fornece métodos para encontrar e adicionar solicitações de pedidos públicos no banco de dados.
/// </summary>
public sealed class PublicOrderRequestRepository(ZFomeDbContext context) : IPublicOrderRequestRepository
{
    public Task<PublicOrderRequest?> FindAsync(Guid tenantId, Guid establishmentId, string key, CancellationToken cancellationToken) => context.PublicOrderRequests.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Key == key, cancellationToken);

    public async Task AddAsync(PublicOrderRequest request, CancellationToken cancellationToken)
    { try { await context.PublicOrderRequests.AddAsync(request, cancellationToken); await context.SaveChangesAsync(cancellationToken); } catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { throw new ConflictException("Idempotency key is already in use."); } }
}

public sealed class PublicOrderTransaction(ZFomeDbContext context) : IPublicOrderTransaction
{
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is not null) return await operation(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            throw new ConflictException("The selected order slot was filled by another order. Choose another available time.");
        }
    }
}
