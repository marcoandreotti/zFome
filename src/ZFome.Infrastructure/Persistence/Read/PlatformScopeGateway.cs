using Microsoft.EntityFrameworkCore;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Infrastructure.Persistence.Write;

namespace ZFome.Infrastructure.Persistence.Read;

public sealed class PlatformScopeGateway(ZFomeDbContext db):IPlatformScopeGateway
{
    public Task<Guid?> FindTenantIdAsync(Guid establishmentId,CancellationToken ct)=>db.Establishments.AsNoTracking().Where(x=>x.Id==establishmentId).Select(x=>(Guid?)x.TenantId).SingleOrDefaultAsync(ct);
}
