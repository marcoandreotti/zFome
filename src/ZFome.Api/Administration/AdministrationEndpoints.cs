using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Customers;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Payments;
using ZFome.Application.Abstractions.Reporting;
using ZFome.Application.Abstractions.Promotions;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Customers;
using ZFome.Application.Identity;
using ZFome.Application.Ordering;
using ZFome.Application.Payments;
using ZFome.Application.Delivery;
using ZFome.Application.Abstractions.Delivery;
using ZFome.Application.Reporting;
using ZFome.Application.Promotions;
using ZFome.Contracts.Administration;
using ZFome.Domain.Ordering;
using ZFome.Domain.Promotions;
using System.Globalization;
using System.Text;

namespace ZFome.Api.Administration;

internal static class AdministrationEndpoints
{
    /// <summary>Registra endpoints autenticados de operação e gestão por estabelecimento.</summary>
    public static IEndpointRouteBuilder MapAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var root=endpoints.MapGroup("/api/admin/establishments/{establishmentId:guid}").WithTags("Administration");
        MapCustomers(root); MapOrders(root); MapCoupons(root); MapPayments(root); MapDelivery(root); MapOrderScheduling(root); MapBusinessDashboard(root);
        NotificationEndpoints.MapNotifications(root);
        return endpoints;
    }

    private static void MapBusinessDashboard(RouteGroupBuilder root)
    {
        var group = root.MapGroup("/reports/dashboard").RequireAuthorization(AdministrativePolicies.Management);
        group.MapGet("", GetBusinessDashboardAsync).Produces<BusinessDashboardResponse>();
        group.MapGet("/export", ExportBusinessDashboardAsync);
    }

    private static async Task<IResult> GetBusinessDashboardAsync(
        Guid establishmentId,
        DateOnly from,
        DateOnly to,
        string? serviceType,
        int? productPage,
        int? productPageSize,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var report = await dispatcher.DispatchAsync<GetBusinessDashboardQuery, BusinessDashboardReadModel>(
            new(establishmentId, from, to, ParseServiceType(serviceType), productPage ?? 1, productPageSize ?? 10),
            cancellationToken);
        return Results.Ok(Map(report));
    }

    private static async Task<IResult> ExportBusinessDashboardAsync(
        Guid establishmentId,
        DateOnly from,
        DateOnly to,
        string? serviceType,
        IQueryDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var report = await dispatcher.DispatchAsync<GetBusinessDashboardQuery, BusinessDashboardReadModel>(
            new(establishmentId, from, to, ParseServiceType(serviceType), ProductPage: 1, ProductPageSize: 100),
            cancellationToken);
        var content = BuildBusinessDashboardCsv(report);
        var filename = $"relatorio-{from:yyyyMMdd}-{to:yyyyMMdd}.csv";
        return Results.File(content, "text/csv; charset=utf-8", filename);
    }

    private static OrderServiceType? ParseServiceType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Enum.TryParse<OrderServiceType>(value, true, out var parsed)
            ? parsed
            : (OrderServiceType)(-1);
    }

    private static BusinessDashboardResponse Map(BusinessDashboardReadModel report) => new(
        report.TimeZoneId,
        Map(report.Current),
        Map(report.Previous),
        report.Series.Select(day => new BusinessDashboardDayResponse(
            day.Date, day.PeriodOffset, day.IsComparison, day.Revenue, day.CompletedOrders, day.ConfirmedPayments)).ToArray(),
        new BusinessDashboardProductPageResponse(
            report.Products.Total,
            report.Products.Page,
            report.Products.PageSize,
            report.Products.Items.Select(product => new BusinessDashboardProductResponse(
                product.Name, product.QuantitySold, product.Revenue)).ToArray()));

    private static BusinessDashboardPeriodResponse Map(BusinessDashboardPeriodReadModel period) => new(
        period.From, period.To, period.Revenue, period.ReceivedOrders, period.CompletedOrders,
        period.AverageTicket, period.ConfirmedPayments, period.CancelledOrders, period.RejectedOrders);

    private static byte[] BuildBusinessDashboardCsv(BusinessDashboardReadModel report)
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        var csv = new StringBuilder("\uFEFFSeção;Data;Indicador;Valor\r\n");
        AddPeriod("Período atual", report.Current);
        AddPeriod("Período anterior", report.Previous);
        foreach (var day in report.Series)
        {
            var period = day.IsComparison ? "Série anterior" : "Série atual";
            AddRow(period, day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Faturamento concluído", day.Revenue.ToString("0.00", culture));
            AddRow(period, day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Pedidos concluídos", day.CompletedOrders.ToString(culture));
            AddRow(period, day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Pagamentos confirmados", day.ConfirmedPayments.ToString("0.00", culture));
        }
        foreach (var product in report.Products.Items)
        {
            AddRow("Produtos", "", product.Name, product.QuantitySold.ToString("0.###", culture));
            AddRow("Produtos", "", $"Faturamento — {product.Name}", product.Revenue.ToString("0.00", culture));
        }
        return Encoding.UTF8.GetBytes(csv.ToString());

        void AddPeriod(string label, BusinessDashboardPeriodReadModel period)
        {
            AddRow(label, $"{period.From:yyyy-MM-dd} a {period.To:yyyy-MM-dd}", "Faturamento concluído", period.Revenue.ToString("0.00", culture));
            AddRow(label, "", "Pedidos recebidos", period.ReceivedOrders.ToString(culture));
            AddRow(label, "", "Pedidos concluídos", period.CompletedOrders.ToString(culture));
            AddRow(label, "", "Ticket médio", period.AverageTicket.ToString("0.00", culture));
            AddRow(label, "", "Pagamentos confirmados", period.ConfirmedPayments.ToString("0.00", culture));
            AddRow(label, "", "Pedidos cancelados", period.CancelledOrders.ToString(culture));
            AddRow(label, "", "Pedidos rejeitados", period.RejectedOrders.ToString(culture));
        }

        void AddRow(string section, string date, string metric, string value) =>
            csv.Append(Csv(section)).Append(';').Append(Csv(date)).Append(';').Append(Csv(metric)).Append(';').Append(Csv(value)).Append("\r\n");

        static string Csv(string value)
        {
            var safe = value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
                ? "'" + value
                : value;
            return '"' + safe.Replace("\"", "\"\"") + '"';
        }
    }

    private static void MapOrderScheduling(RouteGroupBuilder root)
    {
        var group = root.MapGroup("/order-scheduling").RequireAuthorization(AdministrativePolicies.Management);
        group.MapGet("", async (Guid establishmentId, IQueryDispatcher dispatcher, CancellationToken ct) =>
        {
            var config = await dispatcher.DispatchAsync<GetOrderSchedulingConfigurationQuery, OrderSchedulingConfigurationReadModel>(new(establishmentId), ct);
            return Results.Ok(new OrderSchedulingConfigurationResponse(config.TimeZoneId, config.Policies.Select(p => new OrderSchedulingPolicyResponse(p.ServiceType.ToString(), p.IsEnabled, p.SlotIntervalMinutes, p.MinimumAdvanceMinutes, p.HorizonDays, p.MaximumOrdersPerSlot)).ToArray()));
        });
        group.MapPut("/{serviceType}", async (Guid establishmentId, string serviceType, OrderSchedulingPolicyRequest request, ICommandDispatcher dispatcher, CancellationToken ct) =>
        {
            var parsed = Enum.TryParse<OrderServiceType>(serviceType, true, out var value) ? value : (OrderServiceType)(-1);
            await dispatcher.DispatchAsync(new SetOrderSchedulingPolicyCommand(establishmentId, parsed, request.IsEnabled, request.MinimumAdvanceMinutes, request.HorizonDays, request.MaximumOrdersPerSlot), ct);
            return Results.NoContent();
        });
    }

    private static void MapDelivery(RouteGroupBuilder root)
    {
        var group = root.MapGroup("/delivery-regions").RequireAuthorization(AdministrativePolicies.Management);
        group.MapGet("", async (Guid establishmentId, IQueryDispatcher dispatcher, CancellationToken ct) =>
            Results.Ok(await dispatcher.DispatchAsync<ListDeliveryRegionsQuery, IReadOnlyList<DeliveryRegionReadModel>>(new(establishmentId), ct)));
        group.MapPost("", async (Guid establishmentId, DeliveryRegionUpsertRequest request, ICommandDispatcher dispatcher, CancellationToken ct) =>
            Results.Ok(new { id = await dispatcher.DispatchAsync<UpsertDeliveryRegionCommand, Guid>(new(establishmentId, null, request.Name, request.PostalCodeFrom, request.PostalCodeTo, request.Fee, request.EstimatedMinutes), ct) }));
        group.MapPut("/{regionId:guid}", async (Guid establishmentId, Guid regionId, DeliveryRegionUpsertRequest request, ICommandDispatcher dispatcher, CancellationToken ct) =>
            Results.Ok(new { id = await dispatcher.DispatchAsync<UpsertDeliveryRegionCommand, Guid>(new(establishmentId, regionId, request.Name, request.PostalCodeFrom, request.PostalCodeTo, request.Fee, request.EstimatedMinutes), ct) }));
        group.MapPatch("/{regionId:guid}/active", async (Guid establishmentId, Guid regionId, DeliveryRegionActiveRequest request, ICommandDispatcher dispatcher, CancellationToken ct) =>
        { await dispatcher.DispatchAsync(new SetDeliveryRegionActiveCommand(establishmentId, regionId, request.IsActive), ct); return Results.NoContent(); });
    }

    private static void MapCustomers(RouteGroupBuilder root)
    {
        var group=root.MapGroup("/customers").RequireAuthorization(AdministrativePolicies.CustomerOperations);
        group.MapGet("",SearchCustomersAsync).Produces<PagedResponse<CustomerResponse>>();
        group.MapPost("",(Guid establishmentId,CustomerUpsertRequest r,ICommandDispatcher d,CancellationToken ct)=>UpsertCustomerAsync(establishmentId,null,r,d,ct));
        group.MapPut("/{customerId:guid}",(Guid establishmentId,Guid customerId,CustomerUpsertRequest r,ICommandDispatcher d,CancellationToken ct)=>UpsertCustomerAsync(establishmentId,customerId,r,d,ct));
        group.MapPost("/{customerId:guid}/addresses",(Guid establishmentId,Guid customerId,CustomerAddressUpsertRequest r,ICommandDispatcher d,CancellationToken ct)=>UpsertAddressAsync(establishmentId,customerId,null,r,d,ct));
        group.MapPut("/{customerId:guid}/addresses/{addressId:guid}",(Guid establishmentId,Guid customerId,Guid addressId,CustomerAddressUpsertRequest r,ICommandDispatcher d,CancellationToken ct)=>UpsertAddressAsync(establishmentId,customerId,addressId,r,d,ct));
        group.MapDelete("/{customerId:guid}/addresses/{addressId:guid}",RemoveAddressAsync);
    }

    private static void MapOrders(RouteGroupBuilder root)
    {
        var read=root.MapGroup("/orders").RequireAuthorization(AdministrativePolicies.OrderRead);
        read.MapGet("",SearchOrdersAsync).Produces<PagedResponse<OrderSummaryResponse>>(); read.MapGet("/{orderId:guid}",GetOrderAsync).Produces<OrderDetailResponse>();
        root.MapGet("/kitchen", GetKitchenQueueAsync)
            .Produces<IReadOnlyList<KitchenTicketResponse>>()
            .RequireAuthorization(AdministrativePolicies.OrderKitchen);
        root.MapPost("/orders/{orderId:guid}/prepare",(Guid establishmentId,Guid orderId,OrderTransitionRequest r,ICommandDispatcher d,CancellationToken ct)=>TransitionAsync(establishmentId,orderId,OrderStatus.Preparing,r,d,ct)).RequireAuthorization(AdministrativePolicies.OrderKitchen);
        root.MapPost("/orders/{orderId:guid}/ready",(Guid establishmentId,Guid orderId,OrderTransitionRequest r,ICommandDispatcher d,CancellationToken ct)=>TransitionAsync(establishmentId,orderId,OrderStatus.Ready,r,d,ct)).RequireAuthorization(AdministrativePolicies.OrderKitchen);
        root.MapPost("/orders/{orderId:guid}/dispatch",(Guid establishmentId,Guid orderId,OrderTransitionRequest r,ICommandDispatcher d,CancellationToken ct)=>TransitionAsync(establishmentId,orderId,OrderStatus.OutForDelivery,r,d,ct)).RequireAuthorization(AdministrativePolicies.OrderDelivery);
        root.MapPost("/orders/{orderId:guid}/complete",(Guid establishmentId,Guid orderId,OrderTransitionRequest r,ICommandDispatcher d,CancellationToken ct)=>TransitionAsync(establishmentId,orderId,OrderStatus.Completed,r,d,ct)).RequireAuthorization(AdministrativePolicies.OrderCompletion);
        root.MapPost("/orders/{orderId:guid}/cancel",(Guid establishmentId,Guid orderId,OrderTransitionRequest r,ICommandDispatcher d,CancellationToken ct)=>TransitionAsync(establishmentId,orderId,OrderStatus.Cancelled,r,d,ct)).RequireAuthorization(AdministrativePolicies.OrderAttendance);
        root.MapPost("/orders/{orderId:guid}/reject",(Guid establishmentId,Guid orderId,OrderTransitionRequest r,ICommandDispatcher d,CancellationToken ct)=>TransitionAsync(establishmentId,orderId,OrderStatus.Rejected,r,d,ct)).RequireAuthorization(AdministrativePolicies.OrderAttendance);
    }

    private static void MapCoupons(RouteGroupBuilder root)
    {
        var group=root.MapGroup("/coupons").RequireAuthorization(AdministrativePolicies.PromotionManagement);
        group.MapGet("",ListCouponsAsync).Produces<PagedResponse<CouponResponse>>();
        group.MapPost("",(Guid establishmentId,CouponUpsertRequest r,ICommandDispatcher d,CancellationToken ct)=>UpsertCouponAsync(establishmentId,null,r,d,ct));
        group.MapPut("/{couponId:guid}",(Guid establishmentId,Guid couponId,CouponUpsertRequest r,ICommandDispatcher d,CancellationToken ct)=>UpsertCouponAsync(establishmentId,couponId,r,d,ct));
        group.MapPatch("/{couponId:guid}/active",SetCouponActiveAsync);
    }

    private static void MapPayments(RouteGroupBuilder root)
    {
        var methods=root.MapGroup("/payment-methods").RequireAuthorization(AdministrativePolicies.PaymentManagement);
        methods.MapGet("",ListPaymentMethodsAsync).Produces<PagedResponse<PaymentMethodResponse>>();
        methods.MapPost("",(Guid establishmentId,PaymentMethodUpsertRequest r,ICommandDispatcher d,CancellationToken ct)=>UpsertPaymentMethodAsync(establishmentId,null,r,d,ct));
        methods.MapPut("/{methodId:guid}",(Guid establishmentId,Guid methodId,PaymentMethodUpsertRequest r,ICommandDispatcher d,CancellationToken ct)=>UpsertPaymentMethodAsync(establishmentId,methodId,r,d,ct));
        methods.MapPatch("/{methodId:guid}/active",SetPaymentMethodActiveAsync);
        var payments=root.MapGroup("/orders/{orderId:guid}/payments").RequireAuthorization(AdministrativePolicies.PaymentOperations);
        payments.MapGet("",GetPaymentsAsync).Produces<OrderPaymentsResponse>(); payments.MapPost("",CreatePaymentAsync);
        payments.MapPost("/{paymentId:guid}/confirm",ConfirmPaymentAsync); payments.MapPost("/{paymentId:guid}/fail",FailPaymentAsync); payments.MapPost("/{paymentId:guid}/cancel",CancelPaymentAsync);
    }

    private static async Task<IResult> SearchCustomersAsync(Guid establishmentId,string? search,int? page,int? pageSize,IQueryDispatcher d,CancellationToken ct){var currentPage=page??1;var currentSize=pageSize??20;var x=await d.DispatchAsync<SearchCustomersQuery,CustomerSearchResult>(new(establishmentId,search,currentPage,currentSize),ct);return Results.Ok(new PagedResponse<CustomerResponse>(currentPage,currentSize,x.Total,x.Items.Select(Map).ToArray()));}
    private static async Task<IResult> UpsertCustomerAsync(Guid establishmentId,Guid? id,CustomerUpsertRequest r,ICommandDispatcher d,CancellationToken ct)=>Results.Ok(new{id=await d.DispatchAsync<UpsertCustomerCommand,Guid>(new(establishmentId,id,r.Name,r.Phone,r.Email),ct)});
    private static async Task<IResult> UpsertAddressAsync(Guid establishmentId,Guid customerId,Guid? addressId,CustomerAddressUpsertRequest r,ICommandDispatcher d,CancellationToken ct)=>Results.Ok(new{id=await d.DispatchAsync<UpsertCustomerAddressCommand,Guid>(new(establishmentId,customerId,addressId,r.Label,r.Street,r.Number,r.Complement,r.Neighborhood,r.City,r.State,r.PostalCode,r.IsPrimary),ct)});
    private static async Task<IResult> RemoveAddressAsync(Guid establishmentId,Guid customerId,Guid addressId,ICommandDispatcher d,CancellationToken ct){await d.DispatchAsync(new RemoveCustomerAddressCommand(establishmentId,customerId,addressId),ct);return Results.NoContent();}
    private static async Task<IResult> SearchOrdersAsync(Guid establishmentId,DateTimeOffset? from,DateTimeOffset? to,OrderStatus? status,long? number,OrderServiceType? serviceType,int? page,int? pageSize,IQueryDispatcher d,CancellationToken ct){var currentPage=page??1;var currentSize=pageSize??20;var x=await d.DispatchAsync<SearchOrdersQuery,OrderSearchResult>(new(establishmentId,from,to,status,number,serviceType,currentPage,currentSize),ct);return Results.Ok(new PagedResponse<OrderSummaryResponse>(currentPage,currentSize,x.Total,x.Items.Select(o=>new OrderSummaryResponse(o.Id,o.Number,o.ServiceType.ToString(),o.Status.ToString(),o.CustomerName,o.CustomerPhone,o.Total,o.CreatedAt,o.ScheduledAtUtc,o.ScheduledTimeZoneId,(o.Items??[]).Select(i=>new OrderSummaryItemResponse(i.Quantity,i.ProductName,i.VariationName)).ToArray(),o.PreparationAvailable)).ToArray()));}
    private static async Task<IResult> GetOrderAsync(Guid establishmentId,Guid orderId,IQueryDispatcher d,CancellationToken ct)=>Results.Ok(Map(await d.DispatchAsync<GetOrderQuery,OrderReadModel>(new(establishmentId,orderId),ct)));
    private static async Task<IResult> GetKitchenQueueAsync(Guid establishmentId,IQueryDispatcher d,CancellationToken ct)=>Results.Ok((await d.DispatchAsync<GetKitchenQueueQuery,IReadOnlyList<KitchenTicketReadModel>>(new(establishmentId),ct)).Select(Map).ToArray());
    private static async Task<IResult> TransitionAsync(Guid establishmentId,Guid orderId,OrderStatus status,OrderTransitionRequest r,ICommandDispatcher d,CancellationToken ct){await d.DispatchAsync(new TransitionOrderCommand(establishmentId,orderId,status,r.Note),ct);return Results.NoContent();}
    private static async Task<IResult> ListCouponsAsync(Guid establishmentId,string? search,bool? isActive,int? page,int? pageSize,IQueryDispatcher d,CancellationToken ct){var currentPage=page??1;var currentSize=pageSize??20;var x=await d.DispatchAsync<SearchCouponsQuery,CouponSearchResult>(new(establishmentId,search,isActive,currentPage,currentSize),ct);return Results.Ok(new PagedResponse<CouponResponse>(currentPage,currentSize,x.Total,x.Items.Select(c=>new CouponResponse(c.Id,c.Code,c.Description,c.DiscountType.ToString(),c.Value,c.MinimumOrder,c.StartsAt,c.EndsAt,c.MaximumUses,c.UsedCount,c.IsActive)).ToArray()));}
    private static async Task<IResult> UpsertCouponAsync(Guid establishmentId,Guid? id,CouponUpsertRequest r,ICommandDispatcher d,CancellationToken ct){var type=Enum.TryParse<CouponDiscountType>(r.DiscountType,true,out var parsed)?parsed:(CouponDiscountType)(-1);return Results.Ok(new{id=await d.DispatchAsync<UpsertCouponCommand,Guid>(new(establishmentId,id,r.Code,r.Description,type,r.Value,r.MinimumOrder,r.StartsAt,r.EndsAt,r.MaximumUses),ct)});}
    private static async Task<IResult> SetCouponActiveAsync(Guid establishmentId,Guid couponId,SetActiveRequest r,ICommandDispatcher d,CancellationToken ct){await d.DispatchAsync(new SetCouponActiveCommand(establishmentId,couponId,r.IsActive),ct);return Results.NoContent();}
    private static async Task<IResult> ListPaymentMethodsAsync(Guid establishmentId,string? search,bool? isActive,int? page,int? pageSize,IQueryDispatcher d,CancellationToken ct){var currentPage=page??1;var currentSize=pageSize??20;var x=await d.DispatchAsync<SearchPaymentMethodsQuery,PaymentMethodSearchResult>(new(establishmentId,search,isActive,currentPage,currentSize),ct);return Results.Ok(new PagedResponse<PaymentMethodResponse>(currentPage,currentSize,x.Total,x.Items.Select(m=>new PaymentMethodResponse(m.Id,m.Code,m.Name,m.IsOnline,m.AllowsChange,m.IsActive)).ToArray()));}
    private static async Task<IResult> UpsertPaymentMethodAsync(Guid establishmentId,Guid? id,PaymentMethodUpsertRequest r,ICommandDispatcher d,CancellationToken ct)=>Results.Ok(new{id=await d.DispatchAsync<UpsertPaymentMethodCommand,Guid>(new(establishmentId,id,r.Code,r.Name,r.IsOnline,r.AllowsChange),ct)});
    private static async Task<IResult> SetPaymentMethodActiveAsync(Guid establishmentId,Guid methodId,SetActiveRequest r,ICommandDispatcher d,CancellationToken ct){await d.DispatchAsync(new SetPaymentMethodActiveCommand(establishmentId,methodId,r.IsActive),ct);return Results.NoContent();}
    private static async Task<IResult> GetPaymentsAsync(Guid establishmentId,Guid orderId,IQueryDispatcher d,CancellationToken ct)=>Results.Ok(Map(await d.DispatchAsync<GetOrderPaymentsQuery,OrderPaymentsReadModel>(new(establishmentId,orderId),ct)));
    private static async Task<IResult> CreatePaymentAsync(Guid establishmentId,Guid orderId,PaymentCreateRequest r,ICommandDispatcher d,CancellationToken ct)=>Results.Ok(new{id=await d.DispatchAsync<CreatePaymentCommand,Guid>(new(establishmentId,orderId,r.PaymentMethodId,r.Amount,r.ReceivedAmount),ct)});
    private static async Task<IResult> ConfirmPaymentAsync(Guid establishmentId,Guid paymentId,PaymentConfirmRequest r,ICommandDispatcher d,CancellationToken ct)=>Results.Ok(new{id=await d.DispatchAsync<ConfirmPaymentCommand,Guid>(new(establishmentId,paymentId,r.Amount,r.IdempotencyKey,r.ExternalId),ct)});
    private static async Task<IResult> FailPaymentAsync(Guid establishmentId,Guid paymentId,ICommandDispatcher d,CancellationToken ct){await d.DispatchAsync(new FailPaymentCommand(establishmentId,paymentId),ct);return Results.NoContent();}
    private static async Task<IResult> CancelPaymentAsync(Guid establishmentId,Guid paymentId,ICommandDispatcher d,CancellationToken ct){await d.DispatchAsync(new CancelPaymentCommand(establishmentId,paymentId),ct);return Results.NoContent();}
    private static CustomerResponse Map(CustomerReadModel c)=>new(c.Id,c.Name,c.Phone,c.Email,c.Addresses.Select(a=>new CustomerAddressResponse(a.Id,a.Label,a.Street,a.Number,a.Complement,a.Neighborhood,a.City,a.State,a.PostalCode,a.IsPrimary)).ToArray());
    private static OrderDetailResponse Map(OrderReadModel o)=>new(o.Id,o.Number,o.PublicReference,o.ServiceType.ToString(),o.Status.ToString(),o.CustomerName,o.CustomerPhone,o.TableCode,o.Subtotal,o.Discount,o.Fees,o.Total,o.CouponCode,o.ConfirmedAmount,o.IsFullyPaid,o.Items.Select(i=>new AdminOrderItemResponse(i.Id,i.ProductName,i.VariationName,i.UnitPrice,i.Quantity,i.Total,i.Notes,i.Additionals.Select(a=>new AdminOrderAdditionalResponse(a.Name,a.UnitPrice,a.Quantity)).ToArray(),i.BasePrice,i.ModifierGroups?.Select(g=>new AdminOrderModifierGroupResponse(g.GroupId,g.Name,g.PricingStrategy,g.Price,g.Options.Select(x=>new AdminOrderModifierOptionResponse(x.OptionId,x.Name,x.UnitPrice,x.Quantity,x.PortionNumerator,x.PortionDenominator)).ToArray())).ToArray())).ToArray(),o.History.Select(h=>new AdminOrderHistoryResponse(h.PreviousStatus.ToString(),h.NewStatus.ToString(),h.OccurredAt,h.ActorId,h.Note)).ToArray(),o.DeliveryAddress is null ? null : new AdminDeliveryAddressResponse(o.DeliveryAddress.Street,o.DeliveryAddress.Number,o.DeliveryAddress.Complement,o.DeliveryAddress.Neighborhood,o.DeliveryAddress.City,o.DeliveryAddress.State,o.DeliveryAddress.PostalCode),o.DeliveryRegionId,o.DeliveryRegionName,o.DeliveryFee,o.DeliveryEstimatedMinutes,o.ScheduledAtUtc,o.ScheduledTimeZoneId,o.PreparationAvailable);
    private static KitchenTicketResponse Map(KitchenTicketReadModel ticket)=>new(ticket.Id,ticket.Number,ticket.ServiceType.ToString(),ticket.Status.ToString(),ticket.CustomerName,ticket.TableCode,ticket.ConfirmedAt,ticket.PreparationStartedAt,ticket.Action.ToString(),ticket.Items.Select(item=>new KitchenItemResponse(item.Id,item.ProductName,item.VariationName,item.Quantity,item.Notes,item.Additionals.Select(additional=>new KitchenAdditionalResponse(additional.Name,additional.Quantity)).ToArray())).ToArray());
    private static OrderPaymentsResponse Map(OrderPaymentsReadModel o)=>new(o.OrderId,o.DueAmount,o.ConfirmedAmount,o.IsFullyCovered,o.OperationalStatus,o.Payments.Select(p=>new PaymentResponse(p.Id,p.MethodCode,p.MethodName,p.Amount,p.ReceivedAmount,p.Change,p.Status.ToString(),p.ExternalId,p.CreatedAt,p.ConfirmedAt)).ToArray());
}
