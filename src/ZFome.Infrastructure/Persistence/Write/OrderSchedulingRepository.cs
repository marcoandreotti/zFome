using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Exceptions;
using ZFome.Domain.Ordering;

namespace ZFome.Infrastructure.Persistence.Write;

public sealed class OrderSchedulingRepository(ZFomeDbContext context) : IOrderSchedulingRepository
{
    public Task<OrderSchedulingPolicy?> GetPolicyAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, CancellationToken cancellationToken) =>
        context.OrderSchedulingPolicies.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.ServiceType == serviceType, cancellationToken);

    public async Task<IReadOnlyList<OrderSchedulingPolicy>> GetPoliciesAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken) =>
        await context.OrderSchedulingPolicies.AsNoTracking().Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId)
            .OrderBy(x => x.ServiceType).ToListAsync(cancellationToken);

    public Task<string?> GetTimeZoneIdAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken) =>
        context.Establishments.Where(x => x.TenantId == tenantId && x.Id == establishmentId && x.IsActive
                && context.Tenants.Any(tenant => tenant.Id == tenantId && tenant.IsActive))
            .Select(x => x.TimeZoneId).SingleOrDefaultAsync(cancellationToken);

    public Task<int> CountReservedOrdersAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, DateTimeOffset slotStart, CancellationToken cancellationToken) =>
        context.Orders.CountAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.ServiceType == serviceType
            && x.ScheduledAtUtc == slotStart && x.Status != OrderStatus.Cancelled && x.Status != OrderStatus.Rejected, cancellationToken);

    public void Add(OrderSchedulingPolicy policy) => context.OrderSchedulingPolicies.Add(policy);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Order scheduling policy was changed by another operation."); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation })
        { throw new ConflictException("Order scheduling policy conflicts with existing configuration."); }
    }
}
