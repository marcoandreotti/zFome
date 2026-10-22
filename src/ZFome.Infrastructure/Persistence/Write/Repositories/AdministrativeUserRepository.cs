using Microsoft.EntityFrameworkCore;
using ZFome.Application.Abstractions.Identity;
using ZFome.Domain.Identity;

namespace ZFome.Infrastructure.Persistence.Write.Repositories;

/// <summary>
/// Representa um repositório de usuários administrativos que fornece métodos para verificar a existência de e-mails e adicionar novos usuários administrativos.
/// </summary>
public sealed class AdministrativeUserRepository(ZFomeDbContext context) : IAdministrativeUserRepository
{
    public Task<bool> EmailExistsAsync(Guid tenantId, string normalizedEmail, CancellationToken cancellationToken) =>
        context.AdministrativeUsers.AnyAsync(user => user.TenantId == tenantId && user.NormalizedEmail == normalizedEmail, cancellationToken);

    public async Task AddAsync(AdministrativeUser user, CancellationToken cancellationToken)
    { await context.AdministrativeUsers.AddAsync(user, cancellationToken); await context.SaveChangesAsync(cancellationToken); }
}