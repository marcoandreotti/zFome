using Dapper;
using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Identity.Management;

namespace ZFome.Infrastructure.Persistence.Read;

public sealed class AdministrativeUserReadGateway(IReadConnectionFactory connections) : IAdministrativeUserReadGateway
{
    public async Task<bool> CanManageAsync(Guid tenantId, Guid actorId, bool platformUser, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition("""
            select case when @PlatformUser then exists (
                select 1 from identity.platform_user where id = @ActorId and is_active and not password_change_required)
            else exists (select 1 from identity.administrative_user u
                join identity.administrative_user_role r on r.user_id = u.id
                join tenancy.tenant t on t.id = u.tenant_id and t.is_active
                where u.id = @ActorId and u.tenant_id = @TenantId and u.is_active and r.role_id in (1, 2)) end
            """, new { TenantId = tenantId, ActorId = actorId, PlatformUser = platformUser }, cancellationToken: ct));
    }

    public async Task<AdministrativeUserSearchResult> SearchAsync(Guid tenantId, SearchAdministrativeUsersQuery query, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        const string filter = """
            from identity.administrative_user u where u.tenant_id = @TenantId
            and (@Search is null or strpos(lower(u.name), lower(@Search)) > 0 or strpos(lower(u.email), lower(@Search)) > 0)
            and (@IsActive is null or u.is_active = @IsActive)
            and (not @AssociatedOnly or exists (select 1 from identity.user_establishment_access a
                where a.user_id = u.id and a.tenant_id = @TenantId and a.establishment_id = @EstablishmentId and a.is_active))
            """;
        var parameters = new { TenantId = tenantId, Search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(), query.IsActive, query.AssociatedOnly, query.EstablishmentId, query.PageSize, Offset = (query.Page - 1) * query.PageSize };
        // Uma única fotografia evita divergência entre a contagem e os itens durante alterações concorrentes.
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition("select count(*) " + filter, parameters, transaction, cancellationToken: ct));
        var rows = await connection.QueryAsync<AdministrativeUserReadModel>(new CommandDefinition("""
            select u.id as Id, u.name as Name, u.email as Email, u.is_active as IsActive,
                array(select r.role_id from identity.administrative_user_role r where r.user_id = u.id order by r.role_id) as Roles,
                array(select a.establishment_id from identity.user_establishment_access a where a.user_id = u.id and a.tenant_id = @TenantId and a.establishment_id = @EstablishmentId and a.is_active order by a.establishment_id) as EstablishmentIds
            """ + " " + filter + " order by u.name, u.id limit @PageSize offset @Offset", parameters, transaction, cancellationToken: ct));
        await transaction.CommitAsync(ct);
        return new(rows.ToArray(), count, query.Page, query.PageSize);
    }
}
