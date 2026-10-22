namespace ZFome.Application.Abstractions.Reporting;

public interface IBusinessDashboardReadGateway
{
    Task<BusinessDashboardReadModel> GetAsync(
        Guid tenantId,
        Guid establishmentId,
        DateOnly from,
        DateOnly to,
        string? serviceType,
        int productPage,
        int productPageSize,
        CancellationToken cancellationToken);
}

public sealed record BusinessDashboardReadModel(
    string TimeZoneId,
    BusinessDashboardPeriodReadModel Current,
    BusinessDashboardPeriodReadModel Previous,
    IReadOnlyList<BusinessDashboardDayReadModel> Series,
    BusinessDashboardProductPageReadModel Products);

public sealed record BusinessDashboardPeriodReadModel(
    DateOnly From,
    DateOnly To,
    decimal Revenue,
    int ReceivedOrders,
    int CompletedOrders,
    decimal AverageTicket,
    decimal ConfirmedPayments,
    int CancelledOrders,
    int RejectedOrders);

public sealed record BusinessDashboardDayReadModel(
    DateOnly Date,
    int PeriodOffset,
    bool IsComparison,
    decimal Revenue,
    int CompletedOrders,
    decimal ConfirmedPayments);

public sealed record BusinessDashboardProductReadModel(
    string Name,
    decimal QuantitySold,
    decimal Revenue);

public sealed record BusinessDashboardProductPageReadModel(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<BusinessDashboardProductReadModel> Items);
