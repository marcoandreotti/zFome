using Microsoft.EntityFrameworkCore;
using ZFome.Application.Abstractions.Identity;
using ZFome.Domain.Identity;

namespace ZFome.Infrastructure.Persistence.Write;

public sealed class AdministrativeUserManagementRepository(ZFomeDbContext context) : IAdministrativeUserManagementRepository
{
    public Task<AdministrativeUser?> GetAsync(Guid tenantId, Guid userId, CancellationToken ct) =>
        context.AdministrativeUsers.SingleOrDefaultAsync(user => user.TenantId == tenantId && user.Id == userId, ct);

    public Task<bool> IsActiveEstablishmentAsync(Guid tenantId, Guid establishmentId, CancellationToken ct) =>
        context.Establishments.AnyAsync(e => e.TenantId == tenantId && e.Id == establishmentId && e.IsActive && context.Tenants.Any(t => t.Id == tenantId && t.IsActive), ct);

    public Task<bool> IsEligiblePlatformUserAsync(Guid userId, CancellationToken ct) =>
        context.PlatformUsers.AnyAsync(user => user.Id == userId && user.IsActive && !user.PasswordChangeRequired, ct);

    public async Task<(int Owners, int Administrators)> CountOtherAdministratorsAsync(Guid tenantId, Guid excludedUserId, CancellationToken ct)
    {
        var users = context.AdministrativeUsers.Where(user => user.TenantId == tenantId && user.Id != excludedUserId && user.IsActive);
        var owners = await users.CountAsync(user => user.RoleMemberships.Any(role => role.Role == AdministrativeRole.Owner), ct);
        var administrators = await users.CountAsync(user => user.RoleMemberships.Any(role => role.Role == AdministrativeRole.Owner || role.Role == AdministrativeRole.Admin)
            && user.EstablishmentAccesses.Any(access => access.IsActive && context.Establishments.Any(e => e.Id == access.EstablishmentId && e.TenantId == tenantId && e.IsActive)), ct);
        return (owners, administrators);
    }

    public Task<int> CountOtherUnitAdministratorsAsync(Guid tenantId, Guid establishmentId, Guid excludedUserId, CancellationToken ct) =>
        context.AdministrativeUsers.CountAsync(user => user.TenantId == tenantId && user.Id != excludedUserId && user.IsActive
            && user.RoleMemberships.Any(role => role.Role == AdministrativeRole.Owner || role.Role == AdministrativeRole.Admin)
            && user.EstablishmentAccesses.Any(access => access.EstablishmentId == establishmentId && access.IsActive), ct);

    public async Task SaveAsync(CancellationToken ct) => await context.SaveChangesAsync(ct);
}

/// <summary>O bloqueio transacional por Tenant protege contagem, autorização e escrita como uma única operação.</summary>
public sealed class AdministrativeUserManagementTransaction(ZFomeDbContext context) : IAdministrativeUserManagementTransaction
{
    public async Task ExecuteAsync(Guid tenantId, Func<CancellationToken, Task> operation, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"user-management:" + tenantId}, 0))", ct);
        // O escopo HTTP possui contexto novo; leituras rastreadas anteriores não podem decidir a autorização após esperar pelo lock.
        if (context.ChangeTracker.HasChanges())
            throw new InvalidOperationException("User management requires a clean write scope.");
        context.ChangeTracker.Clear();
        await operation(ct);
        await transaction.CommitAsync(ct);
    }
}
