using Dapper;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Platform;

namespace ZFome.Infrastructure.Persistence.Read;

public sealed class PlatformProvisioningReadGateway(IReadConnectionFactory connections) : IPlatformProvisioningReadGateway
{
    public async Task<PlatformTenantSearchResult> SearchTenantsAsync(string? search, bool? isActive,
        int page, int pageSize, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        var filter = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
        const string where = "where (@Search is null or t.name ilike @Search or t.public_code ilike @Search) and (@IsActive is null or t.is_active=@IsActive)";
        var total = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            $"select count(*) from tenancy.tenant t {where}", new { Search = filter, IsActive = isActive }, cancellationToken: ct));
        var tenants = (await connection.QueryAsync<TenantRow>(new CommandDefinition(
            $"select t.id Id,t.name Name,t.public_code PublicCode,t.is_active IsActive from tenancy.tenant t {where} order by t.name,t.id offset @Offset limit @Limit",
            new { Search = filter, IsActive = isActive, Offset = (page - 1) * pageSize, Limit = pageSize }, cancellationToken: ct))).ToArray();
        var units = tenants.Length == 0 ? [] : (await connection.QueryAsync<UnitRow>(new CommandDefinition(
            "select id Id,tenant_id TenantId,trade_name Name,slug Slug,is_active IsActive,(onboarding_completed_at is not null) OnboardingCompleted from tenancy.establishment where tenant_id=any(@Ids) order by trade_name,id",
            new { Ids = tenants.Select(x => x.Id).ToArray() }, cancellationToken: ct))).ToArray();
        var items = tenants.Select(t => new PlatformTenantReadModel(t.Id, t.Name, t.PublicCode, t.IsActive,
            units.Where(x => x.TenantId == t.Id).Select(Map).ToArray())).ToArray();
        return new(items, total, page, pageSize);
    }

    public async Task<PlatformTenantReadModel?> GetTenantAsync(Guid tenantId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        var tenant = await connection.QuerySingleOrDefaultAsync<TenantRow>(new CommandDefinition(
            "select id Id,name Name,public_code PublicCode,is_active IsActive from tenancy.tenant where id=@TenantId",
            new { TenantId = tenantId }, cancellationToken: ct));
        if (tenant is null) return null;
        var units = await connection.QueryAsync<UnitRow>(new CommandDefinition(
            "select id Id,tenant_id TenantId,trade_name Name,slug Slug,is_active IsActive,(onboarding_completed_at is not null) OnboardingCompleted from tenancy.establishment where tenant_id=@TenantId order by trade_name,id",
            new { TenantId = tenantId }, cancellationToken: ct));
        return new(tenant.Id, tenant.Name, tenant.PublicCode, tenant.IsActive, units.Select(Map).ToArray());
    }

    private static PlatformEstablishmentReadModel Map(UnitRow row) =>
        new(row.Id, row.Name, row.Slug, row.IsActive, row.OnboardingCompleted);
    private sealed record TenantRow(Guid Id, string Name, string PublicCode, bool IsActive);
    private sealed record UnitRow(Guid Id, Guid TenantId, string Name, string Slug, bool IsActive, bool OnboardingCompleted);
}
