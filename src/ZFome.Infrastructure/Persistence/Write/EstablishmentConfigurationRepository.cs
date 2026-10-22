using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZFome.Application.Exceptions;
using ZFome.Application.Onboarding;
using ZFome.Domain.Operations;
using ZFome.Domain.Tenancy;
using ZFome.Domain.Identity;

namespace ZFome.Infrastructure.Persistence.Write;

public sealed class EstablishmentConfigurationRepository(ZFomeDbContext context) : IEstablishmentConfigurationRepository
{
    public async Task<Establishment> GetAsync(Guid tenantId, Guid establishmentId, CancellationToken ct) =>
        await context.Establishments.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == establishmentId, ct) ?? throw new NotFoundException("Establishment was not found.");
    public async Task<IReadOnlyList<BusinessHours>> GetHoursAsync(Guid tenantId, Guid establishmentId, CancellationToken ct) =>
        await context.BusinessHours.Where(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId).ToListAsync(ct);
    public void ReplaceHours(IReadOnlyList<BusinessHours> previous, IReadOnlyList<BusinessHours> replacement)
    {
        context.BusinessHours.RemoveRange(previous);
        context.BusinessHours.AddRange(replacement);
    }
    public Task<ServiceTable?> GetTableAsync(Guid tenantId, Guid establishmentId, Guid tableId, CancellationToken ct) =>
        context.ServiceTables.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Id == tableId, ct);
    public Task<ServiceTable?> FindTableIntentAsync(Guid tenantId, Guid establishmentId, Guid intentId, CancellationToken ct) =>
        context.ServiceTables.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.CreationIntent == intentId, ct);
    public void AddTable(ServiceTable table) => context.ServiceTables.Add(table);
    public Task<int> CountAdministratorsAsync(Guid tenantId, Guid establishmentId, CancellationToken ct) =>
        context.AdministrativeUsers.CountAsync(x => x.TenantId == tenantId && x.IsActive && x.RoleMemberships.Any(r => r.Role == AdministrativeRole.Owner || r.Role == AdministrativeRole.Admin)
            && x.EstablishmentAccesses.Any(a => a.EstablishmentId == establishmentId && a.IsActive), ct);
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new ConflictException("The public slug, table code or creation intent is already in use."); }
    }
}
