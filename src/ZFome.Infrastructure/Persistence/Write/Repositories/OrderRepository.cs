using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Exceptions;
using ZFome.Domain.Ordering;

namespace ZFome.Infrastructure.Persistence.Write.Repositories;

/// <summary>
/// Representa um repositório de pedidos que fornece métodos para obter, adicionar e salvar alterações de pedidos no banco de dados.
/// </summary>
/// <param name="context"></param>
public sealed class OrderRepository(ZFomeDbContext context) : IOrderRepository
{
    public Task<Order?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) =>
        context.Orders.Include(x => x.Items).ThenInclude(x => x.Additionals).Include(x => x.Items).ThenInclude(x => x.ModifierGroups).ThenInclude(x => x.Options).Include(x => x.History)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Id == id, cancellationToken);

    public async Task AddAsync(Order order, CancellationToken cancellationToken)
    { await context.Orders.AddAsync(order, cancellationToken); await SaveChangesAsync(cancellationToken); }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Order was changed by another operation."); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        { throw new ConflictException("The selected order slot was filled by another order. Choose another available time."); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { throw new ConflictException("Order number or public reference already exists."); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.CheckViolation }) { throw new ConflictException("Order data violates establishment integrity."); }
    }
}
