using Dapper;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Abstractions.Promotions;
using ZFome.Domain.Promotions;

namespace ZFome.Infrastructure.Persistence.Read;

/// <summary>
/// Representa um gateway de leitura para cupons, fornecendo métodos para pesquisar e listar cupons no banco de dados.
/// </summary>
public sealed class CouponReadGateway(IReadConnectionFactory connectionFactory) : ICouponReadGateway
{
    public async Task<CouponSearchResult> SearchAsync(Guid tenantId, Guid establishmentId, string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken)
    {
        const string sql = """
            select count(*) from promotions.coupon where tenant_id=@TenantId and establishment_id=@EstablishmentId and (@Search is null or code ilike '%'||@Search||'%' or description ilike '%'||@Search||'%') and (@IsActive is null or is_active=@IsActive);
            select id,code,description,discount_type as DiscountType,value,minimum_order as MinimumOrder,starts_at as StartsAt,ends_at as EndsAt,maximum_uses as MaximumUses,used_count as UsedCount,is_active as IsActive
            from promotions.coupon where tenant_id=@TenantId and establishment_id=@EstablishmentId and (@Search is null or code ilike '%'||@Search||'%' or description ilike '%'||@Search||'%') and (@IsActive is null or is_active=@IsActive)
            order by code,id offset @Offset rows fetch next @PageSize rows only;
            """;
        var parameters = new { TenantId = tenantId, EstablishmentId = establishmentId, Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(), IsActive = isActive, Offset = (page - 1) * pageSize, PageSize = pageSize }; await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken); using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)); var total = await grid.ReadSingleAsync<int>(); var rows = (await grid.ReadAsync<Row>()).ToArray(); return new(total, rows.Select(Map).ToArray());
    }

    public async Task<IReadOnlyList<CouponReadModel>> ListAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken)
    {
        const string sql = """
            select id, code, description, discount_type as DiscountType, value, minimum_order as MinimumOrder,
                   starts_at as StartsAt, ends_at as EndsAt, maximum_uses as MaximumUses, used_count as UsedCount, is_active as IsActive
            from promotions.coupon
            where tenant_id = @TenantId and establishment_id = @EstablishmentId
            order by code, id;
            """;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(sql, new { TenantId = tenantId, EstablishmentId = establishmentId }, cancellationToken: cancellationToken));
        return rows.Select(Map).ToArray();
    }

    private static CouponReadModel Map(Row x) => new(x.Id, x.Code, x.Description, Enum.Parse<CouponDiscountType>(x.DiscountType), x.Value, x.MinimumOrder, x.StartsAt, x.EndsAt, x.MaximumUses, x.UsedCount, x.IsActive);

    private sealed class Row
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string DiscountType { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public decimal MinimumOrder { get; set; }
        public DateTimeOffset StartsAt { get; set; }
        public DateTimeOffset EndsAt { get; set; }
        public int? MaximumUses { get; set; }
        public int UsedCount { get; set; }
        public bool IsActive { get; set; }
    }
}