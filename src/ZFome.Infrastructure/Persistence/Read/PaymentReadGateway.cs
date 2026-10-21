using Dapper;
using ZFome.Application.Abstractions.Payments;
using ZFome.Application.Abstractions.Persistence;
using ZFome.Domain.Payments;

namespace ZFome.Infrastructure.Persistence.Read;

/// <summary>
/// Representa um gateway de leitura para métodos de pagamento, fornecendo operações de consulta relacionadas a métodos de pagamento e pagamentos de pedidos.
/// </summary>
public sealed class PaymentReadGateway(IReadConnectionFactory connectionFactory) : IPaymentReadGateway
{
    public async Task<PaymentMethodSearchResult> SearchMethodsAsync(Guid tenantId, Guid establishmentId, string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken)
    { const string sql = """
        select count(*) from payments.payment_method where tenant_id=@TenantId and establishment_id=@EstablishmentId and (@Search is null or code ilike '%'||@Search||'%' or name ilike '%'||@Search||'%') and (@IsActive is null or is_active=@IsActive);
        select id,code,name,is_online as IsOnline,allows_change as AllowsChange,is_active as IsActive from payments.payment_method where tenant_id=@TenantId and establishment_id=@EstablishmentId and (@Search is null or code ilike '%'||@Search||'%' or name ilike '%'||@Search||'%') and (@IsActive is null or is_active=@IsActive) order by name,id offset @Offset rows fetch next @PageSize rows only;
        """; var parameters = new { TenantId = tenantId, EstablishmentId = establishmentId, Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(), IsActive = isActive, Offset = (page - 1) * pageSize, PageSize = pageSize }; await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken); using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)); return new(await grid.ReadSingleAsync<int>(), (await grid.ReadAsync<PaymentMethodReadModel>()).ToArray()); }

    public async Task<IReadOnlyList<PaymentMethodReadModel>> ListMethodsAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken)
    { const string sql = "select id, code, name, is_online as IsOnline, allows_change as AllowsChange, is_active as IsActive from payments.payment_method where tenant_id=@TenantId and establishment_id=@EstablishmentId order by name,id;"; await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken); return (await connection.QueryAsync<PaymentMethodReadModel>(new CommandDefinition(sql, new { TenantId = tenantId, EstablishmentId = establishmentId }, cancellationToken: cancellationToken))).ToArray(); }

    public async Task<OrderPaymentsReadModel> GetOrderPaymentsAsync(Guid tenantId, Guid establishmentId, Guid orderId, CancellationToken cancellationToken)
    {
        const string sql = """
            select id as OrderId, total as DueAmount, status as OperationalStatus from orders."order" where tenant_id=@TenantId and establishment_id=@EstablishmentId and id=@OrderId;
            select id, payment_method_code as MethodCode, payment_method_name as MethodName, amount, received_amount as ReceivedAmount, change, status, external_id as ExternalId, created_at as CreatedAt, confirmed_at as ConfirmedAt
            from payments.payment where tenant_id=@TenantId and establishment_id=@EstablishmentId and order_id=@OrderId order by created_at,id;
            """;
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken); using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, EstablishmentId = establishmentId, OrderId = orderId }, cancellationToken: cancellationToken));
        var order = await grid.ReadSingleOrDefaultAsync<OrderRow>() ?? throw new KeyNotFoundException("Order was not found."); var rows = (await grid.ReadAsync<PaymentRow>()).ToArray(); var confirmed = rows.Where(x => x.Status == nameof(PaymentStatus.Confirmed)).Sum(x => x.Amount);
        return new(order.OrderId, order.DueAmount, confirmed, confirmed >= order.DueAmount, order.OperationalStatus, rows.Select(x => new PaymentReadModel(x.Id, x.MethodCode, x.MethodName, x.Amount, x.ReceivedAmount, x.Change, Enum.Parse<PaymentStatus>(x.Status), x.ExternalId, x.CreatedAt, x.ConfirmedAt)).ToArray());
    }

    private sealed record OrderRow(Guid OrderId, decimal DueAmount, string OperationalStatus);

    private sealed class PaymentRow
    { public Guid Id { get; set; } public string MethodCode { get; set; } = string.Empty; public string MethodName { get; set; } = string.Empty; public decimal Amount { get; set; } public decimal? ReceivedAmount { get; set; } public decimal Change { get; set; } public string Status { get; set; } = string.Empty; public string? ExternalId { get; set; } public DateTimeOffset CreatedAt { get; set; } public DateTimeOffset? ConfirmedAt { get; set; } }
}