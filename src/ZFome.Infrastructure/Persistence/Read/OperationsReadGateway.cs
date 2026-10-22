using Dapper;
using ZFome.Application.Abstractions.Operations;
using ZFome.Application.Abstractions.Persistence;

namespace ZFome.Infrastructure.Persistence.Read;

/// <summary>
/// Representa um gateway de leitura para operações, fornecendo métodos para resolver informações de tabelas e verificar se um estabelecimento está aberto em um determinado horário.
/// </summary>
public sealed class OperationsReadGateway(IReadConnectionFactory connectionFactory) : IOperationsReadGateway
{
    public async Task<TableContext?> ResolveTableAsync(string normalizedSlug, string token, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<TableContext>(new CommandDefinition(
            """select e.tenant_id as TenantId,e.id as EstablishmentId,t.id as TableId,t.code as Code from tenancy.establishment e join tenancy.tenant tenant on tenant.id=e.tenant_id join operations.service_table t on t.establishment_id=e.id and t.tenant_id=e.tenant_id where e.slug=@Slug and t.qr_code_token=@Token and e.is_active=true and tenant.is_active=true and t.is_active=true""",
            new { Slug = normalizedSlug, Token = token }, cancellationToken: cancellationToken));
    }

    public async Task<bool> IsOpenAsync(Guid tenantId, Guid establishmentId, DayOfWeek day, TimeOnly time, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """select exists(select 1 from operations.business_hours where tenant_id=@TenantId and establishment_id=@EstablishmentId and day_of_week=@Day and opens_at<=@Time and closes_at>@Time and is_active=true)""",
            new { TenantId = tenantId, EstablishmentId = establishmentId, Day = (short)day, Time = time.ToTimeSpan() }, cancellationToken: cancellationToken));
    }
}