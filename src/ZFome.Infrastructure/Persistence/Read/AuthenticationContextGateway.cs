using Dapper;
using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Identity.Authentication;
using ZFome.Domain.Identity;

namespace ZFome.Infrastructure.Persistence.Read;

public sealed class AuthenticationContextGateway(IReadConnectionFactory connections) : IAuthenticationContextGateway
{
    public async Task<IReadOnlyCollection<AuthenticationEstablishment>> GetEstablishmentsAsync(AuthenticatedIdentity identity, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AuthenticationEstablishment>(new CommandDefinition(
            """
            select e.id as Id, e.trade_name as Name
            from tenancy.establishment e
            join tenancy.tenant t on t.id = e.tenant_id and t.is_active
            where e.is_active and (@IsPlatformUser or (e.tenant_id = @TenantId and exists (
                select 1 from identity.user_establishment_access a
                where a.user_id = @IdentityId and a.tenant_id = e.tenant_id
                    and a.establishment_id = e.id and a.is_active)))
            order by e.trade_name, e.id
            """, new { IsPlatformUser = identity.Type == AuthenticationIdentityType.PlatformUser, identity.TenantId, identity.IdentityId }, cancellationToken: cancellationToken));
        return rows.ToArray();
    }
}
