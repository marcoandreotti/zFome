using ZFome.Domain.Identity;

namespace ZFome.Application.Abstractions.Identity;

/// <summary>
/// Representa um repositório de usuários administrativos que fornece métodos para verificar a existência de e-mails e adicionar novos usuários administrativos.
/// </summary>
public interface IAdministrativeUserRepository
{
    Task<bool> EmailExistsAsync(Guid tenantId, string normalizedEmail, CancellationToken cancellationToken);

    Task AddAsync(AdministrativeUser user, CancellationToken cancellationToken);
}