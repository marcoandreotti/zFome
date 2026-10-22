using Microsoft.EntityFrameworkCore;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Domain.Operations;
using ZFome.Domain.Ordering;

namespace ZFome.Infrastructure.Persistence.Write;

public sealed class OrderAvailabilityGateway(ZFomeDbContext context) : IOrderAvailabilityGateway
{
    public async Task<AvailabilityDecision> EvaluateAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, DateTimeOffset instant, CancellationToken cancellationToken)
    {
        var establishment = await context.Establishments.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == establishmentId)
            .Select(x => new { x.IsActive, x.TimeZoneId, TenantActive = context.Tenants.Any(t => t.Id == tenantId && t.IsActive) })
            .SingleOrDefaultAsync(cancellationToken);
        var hours = await context.BusinessHours.AsNoTracking().Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.IsActive).ToListAsync(cancellationToken);
        var fromDate = DateOnly.FromDateTime(instant.UtcDateTime).AddDays(-2);
        var toDate = DateOnly.FromDateTime(instant.UtcDateTime).AddDays(16);
        var exceptions = await context.ServiceScheduleExceptions.AsNoTracking().Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Date >= fromDate && x.Date <= toDate).ToListAsync(cancellationToken);
        var pauses = await context.ServicePauses.AsNoTracking().Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.CancelledAt == null && x.StartsAt <= instant.AddDays(15) && (x.EndsAt == null || x.EndsAt > instant)).ToListAsync(cancellationToken);
        return AvailabilityEvaluator.Evaluate(establishment is { IsActive: true, TenantActive: true }, establishment?.TimeZoneId ?? "UTC", hours, exceptions, pauses, serviceType, instant);
    }
}
