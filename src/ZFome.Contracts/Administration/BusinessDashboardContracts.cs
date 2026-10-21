namespace ZFome.Contracts.Administration;

public sealed record BusinessDashboardResponse(
    string TimeZoneId,
    BusinessDashboardPeriodResponse Current,
    BusinessDashboardPeriodResponse Previous,
    IReadOnlyList<BusinessDashboardDayResponse> Series,
    BusinessDashboardProductPageResponse Products);

public sealed record BusinessDashboardPeriodResponse(
    DateOnly From,
    DateOnly To,
    decimal Revenue,
    int ReceivedOrders,
    int CompletedOrders,
    decimal AverageTicket,
    decimal ConfirmedPayments,
    int CancelledOrders,
    int RejectedOrders);

public sealed record BusinessDashboardDayResponse(
    DateOnly Date,
    int PeriodOffset,
    bool IsComparison,
    decimal Revenue,
    int CompletedOrders,
    decimal ConfirmedPayments);

public sealed record BusinessDashboardProductResponse(
    string Name,
    decimal QuantitySold,
    decimal Revenue);

public sealed record BusinessDashboardProductPageResponse(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<BusinessDashboardProductResponse> Items);
