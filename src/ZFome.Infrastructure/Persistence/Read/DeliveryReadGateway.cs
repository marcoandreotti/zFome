using Dapper;
using ZFome.Application.Abstractions.Delivery;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Exceptions;
using ZFome.Domain.Delivery;
using ZFome.Domain.SharedKernel;

namespace ZFome.Infrastructure.Persistence.Read;

public sealed class DeliveryReadGateway(IReadConnectionFactory connections) : IDeliveryReadGateway
{
    public async Task<IReadOnlyList<DeliveryRegionReadModel>> ListAsync(Guid tenantId, Guid establishmentId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        var rows = await connection.QueryAsync<DeliveryRegionReadModel>(new CommandDefinition("""
            select id, name, postal_code_from as PostalCodeFrom, postal_code_to as PostalCodeTo,
                   fee, estimated_minutes as EstimatedMinutes, is_active as IsActive
            from delivery.delivery_region
            where tenant_id=@TenantId and establishment_id=@EstablishmentId
            order by postal_code_from, name
            """, new { TenantId = tenantId, EstablishmentId = establishmentId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<DeliveryQuote?> FindQuoteAsync(Guid tenantId, Guid establishmentId, string normalizedPostalCode, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        var rows = (await connection.QueryAsync<RegionRow>(new CommandDefinition("""
            select id, name, fee, estimated_minutes as EstimatedMinutes
            from delivery.delivery_region
            where tenant_id=@TenantId and establishment_id=@EstablishmentId and is_active
              and postal_code_from<=@PostalCode and postal_code_to>=@PostalCode
            order by postal_code_from, id
            limit 2
            """, new { TenantId = tenantId, EstablishmentId = establishmentId, PostalCode = normalizedPostalCode }, cancellationToken: ct))).AsList();
        if (rows.Count > 1) throw new ConflictException("Delivery coverage configuration is ambiguous.");
        return rows.Count == 0 ? null : new DeliveryQuote(rows[0].Id, rows[0].Name, new Money(rows[0].Fee), rows[0].EstimatedMinutes);
    }

    private sealed record RegionRow(Guid Id, string Name, decimal Fee, int EstimatedMinutes);
}
