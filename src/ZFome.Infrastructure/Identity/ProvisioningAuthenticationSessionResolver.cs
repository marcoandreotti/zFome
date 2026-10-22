using Microsoft.EntityFrameworkCore;
using ZFome.Application.Abstractions.Identity;
using ZFome.Domain.Identity;
using ZFome.Infrastructure.Persistence.Write;

namespace ZFome.Infrastructure.Identity;

public sealed class ProvisioningAuthenticationSessionResolver(ZFomeDbContext db,
    IAuthenticationSecretProtector secrets, TimeProvider clock) : IAuthenticationSessionResolver
{
    public async Task<AuthenticatedIdentity?> ResolveAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = secrets.Hash(token);
        var session = await db.AdministrativeSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.AccessTokenHash == hash, ct);
        if (session is null || !session.IsAccessValid(clock.GetUtcNow())) return null;
        if (session.IdentityType == AuthenticationIdentityType.PlatformUser)
        {
            var platform = await db.PlatformUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == session.IdentityId && x.IsActive, ct);
            return platform is null ? null : new(session.Id, session.IdentityType, session.IdentityId, null, [], [], platform.PasswordChangeRequired);
        }
        if (session.TenantId is not Guid tenantId || !await db.Tenants.AnyAsync(x => x.Id == tenantId && x.IsActive, ct)) return null;
        var user = await db.AdministrativeUsers.AsNoTracking().Include(x => x.RoleMemberships).Include(x => x.EstablishmentAccesses)
            .SingleOrDefaultAsync(x => x.Id == session.IdentityId && x.TenantId == tenantId && x.IsActive, ct);
        return user is null ? null : new(session.Id, session.IdentityType, session.IdentityId, tenantId,
            user.RoleMemberships.Select(x => x.Role).ToArray(),
            user.EstablishmentAccesses.Where(x => x.IsActive).Select(x => x.EstablishmentId).ToArray(),
            user.PasswordChangeRequired);
    }
}
