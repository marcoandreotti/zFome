using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZFome.Application.Abstractions.Catalog;
using ZFome.Application.Abstractions.Operations;
using ZFome.Application.Exceptions;
using ZFome.Domain.Catalog;
using ZFome.Domain.Operations;
using ZFome.Domain.Ordering;

namespace ZFome.Infrastructure.Persistence.Write;

public sealed class AvailabilityRepository(ZFomeDbContext context) : IAvailabilityRepository
{
    public async Task<IReadOnlyList<ServiceScheduleException>> GetExceptionsAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken) =>
        await context.ServiceScheduleExceptions.Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId).ToListAsync(cancellationToken);

    public void ReplaceExceptions(IReadOnlyCollection<ServiceScheduleException> previous, IReadOnlyCollection<ServiceScheduleException> replacement)
    {
        context.ServiceScheduleExceptions.RemoveRange(previous);
        context.ServiceScheduleExceptions.AddRange(replacement);
    }

    public Task<ServicePause?> GetActivePauseAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, DateTimeOffset instant, CancellationToken cancellationToken) =>
        context.ServicePauses.Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.ServiceType == serviceType
            && x.CancelledAt == null && x.StartsAt <= instant && (x.EndsAt == null || x.EndsAt > instant))
            .OrderByDescending(x => x.StartsAt).FirstOrDefaultAsync(cancellationToken);

    public void AddPause(ServicePause pause) => context.ServicePauses.Add(pause);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => SaveAsync(context, cancellationToken);

    internal static async Task SaveAsync(ZFomeDbContext context, CancellationToken cancellationToken)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.CheckViolation })
        { throw new ConflictException("Availability configuration conflicts with existing data."); }
    }
}

public sealed class OfferAvailabilityRepository(ZFomeDbContext context) : IOfferAvailabilityRepository
{
    public Task<bool> OfferExistsAsync(Guid tenantId, Guid establishmentId, OfferKind kind, Guid offerId, CancellationToken cancellationToken) => kind switch
    {
        OfferKind.Product => context.Products.AnyAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Id == offerId, cancellationToken),
        OfferKind.Variation => context.Products.AnyAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Variations.Any(v => v.Id == offerId), cancellationToken),
        OfferKind.Additional => context.Additionals.AnyAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Id == offerId, cancellationToken),
        _ => Task.FromResult(false)
    };

    public Task<OfferUnavailability?> GetActiveAsync(Guid tenantId, Guid establishmentId, OfferKind kind, Guid offerId, DateTimeOffset instant, CancellationToken cancellationToken) =>
        context.OfferUnavailabilities.Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Kind == kind && x.OfferId == offerId
            && x.ReactivatedAt == null && x.StartsAt <= instant && (x.EndsAt == null || x.EndsAt > instant))
            .OrderByDescending(x => x.StartsAt).FirstOrDefaultAsync(cancellationToken);

    public void Add(OfferUnavailability value) => context.OfferUnavailabilities.Add(value);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => AvailabilityRepository.SaveAsync(context, cancellationToken);
}
