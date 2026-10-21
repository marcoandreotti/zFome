using Dapper;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Abstractions.Tenancy;

namespace ZFome.Infrastructure.Persistence.Read;

/// <summary>
/// Representa um gateway de acesso a estabelecimentos que fornece métodos para verificar se um usuário tem acesso ativo a um estabelecimento específico.
/// </summary>
public sealed class EstablishmentAccessGateway(IReadConnectionFactory connectionFactory) : IEstablishmentAccessGateway
{
    public async Task<bool> HasActiveAccessAsync(Guid tenantId, Guid userId, Guid establishmentId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """select exists(select 1 from identity.administrative_user u join identity.user_establishment_access a on a.user_id=u.id join tenancy.establishment e on e.id=a.establishment_id and e.tenant_id=a.tenant_id where u.id=@UserId and u.tenant_id=@TenantId and u.is_active=true and a.establishment_id=@EstablishmentId and a.is_active=true and e.is_active=true)""",
            new { TenantId = tenantId, UserId = userId, EstablishmentId = establishmentId }, cancellationToken: cancellationToken));
    }
}