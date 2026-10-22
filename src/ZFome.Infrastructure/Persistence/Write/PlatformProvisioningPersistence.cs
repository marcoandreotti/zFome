using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Exceptions;
using ZFome.Domain.Identity;
using ZFome.Domain.Tenancy;

namespace ZFome.Infrastructure.Persistence.Write;

public sealed class PlatformProvisioningRepository(ZFomeDbContext db) : IPlatformProvisioningRepository
{
    public Task<PlatformProvisioningIntent?> GetIntentAsync(Guid actorId, Guid key, CancellationToken ct) =>
        db.Set<PlatformProvisioningIntent>().SingleOrDefaultAsync(x => x.ActorId == actorId && x.Key == key, ct);
    public Task<bool> TenantPublicCodeExistsAsync(string publicCode, CancellationToken ct) =>
        db.Tenants.AnyAsync(x => x.PublicCode == publicCode, ct);
    public Task<bool> EstablishmentSlugExistsAsync(string slug, CancellationToken ct) =>
        db.Establishments.AnyAsync(x => x.Slug == new Slug(slug), ct);
    public Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken ct) => db.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId, ct);
    public Task<AdministrativeUser?> GetOwnerAsync(Guid tenantId, Guid ownerId, CancellationToken ct) =>
        db.AdministrativeUsers.Include(x => x.RoleMemberships).Include(x => x.EstablishmentAccesses)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == ownerId && x.IsActive &&
                x.RoleMemberships.Any(role => role.Role == AdministrativeRole.Owner), ct);

    public Task AddTenantProvisioningAsync(Tenant tenant, Establishment establishment, AdministrativeUser owner,
        PlatformProvisioningIntent intent, CancellationToken ct)
    { db.AddRange(tenant, establishment, owner, intent); return Task.CompletedTask; }

    public Task AddEstablishmentProvisioningAsync(Establishment establishment, PlatformProvisioningIntent intent,
        CancellationToken ct)
    { db.AddRange(establishment, intent); return Task.CompletedTask; }
}

public sealed class PlatformProvisioningTransaction(ZFomeDbContext db) : IPlatformProvisioningTransaction
{
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await operation(ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(ct);
            throw new ConflictException("Provisioning data conflicts with an existing resource.");
        }
    }
}
