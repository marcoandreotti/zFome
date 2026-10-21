using ZFome.Application.Abstractions.Catalog;
using ZFome.Application.Abstractions.Communications;
using ZFome.Application.Abstractions.Customers;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Payments;
using ZFome.Application.Abstractions.Promotions;
using ZFome.Application.Abstractions.PublicOrdering;
using ZFome.Application.Abstractions.Operations;
using ZFome.Application.Abstractions.Delivery;
using ZFome.Application.Exceptions;
using ZFome.Application.PublicOrdering;
using ZFome.Domain.Ordering;
using ZFome.Domain.Payments;
using ZFome.Domain.Promotions;
using ZFome.Domain.SharedKernel;
using ZFome.Domain.Operations;
using ZFome.Domain.Delivery;

namespace ZFome.Application.Tests.PublicOrdering;

public sealed class PublicOrderingApplicationTests
{
    [Fact]
    public async Task Inactive_or_unknown_slug_is_not_revealed()
    {
        var handler=new GetPublicContextQueryHandler(new ContextGateway(null),new AvailabilityGateway(),TimeProvider.System);
        await Assert.ThrowsAsync<NotFoundException>(()=>handler.HandleAsync(new("closed",null),CancellationToken.None));
    }

    [Fact]
    public async Task Simulation_uses_authoritative_offer_price()
    {
        var context=Scope();
        var handler=new SimulatePublicOrderQueryHandler(new ContextGateway(context),new OfferResolver(25m),new CustomerResolver(),new TableResolver(),new CouponRepository(),new PaymentMethodRepository(),new AvailabilityGateway(),new DeliveryGateway(),new SchedulingRepository(),TimeProvider.System);
        var result=await handler.HandleAsync(new("unit-a",OrderServiceType.Pickup,null,null,null,null,null,null,[new(Guid.NewGuid(),null,2,null,[])]),CancellationToken.None);
        Assert.Equal(50m,result.Total); Assert.Equal(25m,result.Items.Single().UnitPrice);
    }

    [Fact]
    public async Task Delivery_simulation_uses_server_region_fee_and_estimate()
    {
        var scope = Scope();
        var quote = new DeliveryQuote(Guid.NewGuid(), "Centro", new Money(7.50m), 35);
        var handler = new SimulatePublicOrderQueryHandler(new ContextGateway(scope), new OfferResolver(25m), new CustomerResolver(), new TableResolver(), new CouponRepository(), new PaymentMethodRepository(), new AvailabilityGateway(), new DeliveryGateway(quote), new SchedulingRepository(), TimeProvider.System);
        var address = new PublicAddress("Casa", "Rua A", "1", null, "Centro", "São Paulo", "SP", "01000-000");

        var result = await handler.HandleAsync(new("unit-a", OrderServiceType.Delivery, null, null, null, address, null, null, [new(Guid.NewGuid(), null, 2, null, [])]), CancellationToken.None);

        Assert.Equal(57.50m, result.Total);
        Assert.Equal(7.50m, result.DeliveryFee);
        Assert.Equal("Centro", result.DeliveryRegionName);
        Assert.Equal(35, result.DeliveryEstimatedMinutes);
        Assert.NotNull(result.DeliveryQuoteIssuedAt);
    }

    [Fact]
    public async Task Delivery_simulation_rejects_addresses_outside_coverage_without_exposing_policy()
    {
        var scope = Scope();
        var handler = new SimulatePublicOrderQueryHandler(new ContextGateway(scope), new OfferResolver(25m), new CustomerResolver(), new TableResolver(), new CouponRepository(), new PaymentMethodRepository(), new AvailabilityGateway(), new DeliveryGateway(), new SchedulingRepository(), TimeProvider.System);
        var address = new PublicAddress("Casa", "Rua A", "1", null, "Centro", "São Paulo", "SP", "01000-000");

        var exception = await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(
            new("unit-a", OrderServiceType.Delivery, null, null, null, address, null, null, [new(Guid.NewGuid(), null, 1, null, [])]), CancellationToken.None));

        Assert.Equal("Delivery is unavailable for this address.", exception.Message);
    }

    [Fact]
    public void Confirmation_requires_strong_idempotency_key()
    {
        var validator=new ConfirmPublicOrderCommandValidator();
        var command=new ConfirmPublicOrderCommand("unit-a","short",OrderServiceType.Pickup,null,null,null,null,null,Guid.NewGuid(),null,[new(Guid.NewGuid(),null,1,null,[])]);
        var result=validator.Validate(command);
        Assert.Contains(result.Errors,x=>x.PropertyName=="IdempotencyKey");
    }

    [Fact]
    public void Tracking_reference_rejects_enumerable_identifiers()
    {
        var query=new GetPublicOrderQuery(Guid.NewGuid().ToString());
        var result=new GetPublicOrderQueryValidator().Validate(query);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Repeated_confirmation_returns_original_order_without_duplication()
    {
        var scope=Scope(); var method=PaymentMethod.Create(scope.TenantId,scope.EstablishmentId,"PIX","Pix",true,false,DateTimeOffset.UtcNow);
        var orders=new OrderRepository(); var requests=new RequestRepository();
        var customerNotifications = new CustomerNotificationStager();
        var handler=new ConfirmPublicOrderCommandHandler(new ContextGateway(scope),new OfferResolver(20m),new CustomerResolver(),new TableResolver(),new CouponRepository(),new ActivePaymentMethodRepository(method),new PaymentRepository(),orders,new Sequence(),requests,new Transaction(),new AvailabilityGateway(),new DeliveryGateway(),new SchedulingRepository(),customerNotifications,new Publisher(),TimeProvider.System);
        var command=new ConfirmPublicOrderCommand("unit-a","confirmation-key-123",OrderServiceType.Pickup,null,null,null,null,null,method.Id,null,[new(Guid.NewGuid(),null,1,null,[])]);
        var first=await handler.HandleAsync(command,CancellationToken.None); var replay=await handler.HandleAsync(command,CancellationToken.None);
        Assert.Equal(first,replay); Assert.Equal(1,orders.AddCount); Assert.Equal(1, customerNotifications.Calls);
    }

    [Fact]
    public async Task Confirmation_preserves_the_authoritatively_validated_schedule()
    {
        var scope = Scope();
        var method = PaymentMethod.Create(scope.TenantId, scope.EstablishmentId, "PIX", "Pix", true, false, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var slot = DateTimeOffset.FromUnixTimeSeconds((now.ToUnixTimeSeconds() / 1800 + 5) * 1800);
        var scheduling = new SchedulingRepository(
            OrderSchedulingPolicy.Create(scope.TenantId, scope.EstablishmentId, OrderServiceType.Pickup, true),
            "UTC");
        var orders = new OrderRepository();
        var handler = new ConfirmPublicOrderCommandHandler(
            new ContextGateway(scope), new OfferResolver(20m), new CustomerResolver(), new TableResolver(),
            new CouponRepository(), new ActivePaymentMethodRepository(method), new PaymentRepository(), orders,
            new Sequence(), new RequestRepository(), new Transaction(), new AvailabilityClosedUntilFutureGateway(), new DeliveryGateway(),
            scheduling, new CustomerNotificationStager(), new Publisher(), TimeProvider.System);
        var command = new ConfirmPublicOrderCommand("unit-a", "scheduled-order-key-123", OrderServiceType.Pickup,
            null, null, null, null, null, method.Id, null, [new(Guid.NewGuid(), null, 1, null, [])],
            ScheduledAtUtc: slot);

        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(slot, orders.LastOrder!.ScheduledAtUtc);
        Assert.Equal("UTC", orders.LastOrder.ScheduledTimeZoneId);
    }

    [Fact]
    public async Task Delivery_confirmation_rejects_changed_quote_and_returns_authoritative_total()
    {
        var scope = Scope();
        var method = PaymentMethod.Create(scope.TenantId, scope.EstablishmentId, "PIX", "Pix", true, false, DateTimeOffset.UtcNow);
        var regionId = Guid.NewGuid();
        var quote = new DeliveryQuote(regionId, "Centro", new Money(7.50m), 35);
        var handler = new ConfirmPublicOrderCommandHandler(new ContextGateway(scope), new OfferResolver(20m), new CustomerResolver(), new TableResolver(), new CouponRepository(), new ActivePaymentMethodRepository(method), new PaymentRepository(), new OrderRepository(), new Sequence(), new RequestRepository(), new Transaction(), new AvailabilityGateway(), new DeliveryGateway(quote), new SchedulingRepository(), new CustomerNotificationStager(), new Publisher(), TimeProvider.System);
        var address = new PublicAddress("Casa", "Rua A", "1", null, "Centro", "São Paulo", "SP", "01000-000");
        var command = new ConfirmPublicOrderCommand("unit-a", "delivery-quote-key-123", OrderServiceType.Delivery, null, null, null, address, null, method.Id, null,
            [new(Guid.NewGuid(), null, 1, null, [])], regionId, 3m, 35, DateTimeOffset.UtcNow);

        var exception = await Assert.ThrowsAsync<DeliveryQuoteConflictException>(() => handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal(7.50m, exception.CurrentFee);
        Assert.Equal(27.50m, exception.CurrentTotal);
    }

    [Fact]
    public async Task Confirmation_revalidates_availability_and_persists_nothing_for_stale_cart()
    {
        var scope=Scope(); var method=PaymentMethod.Create(scope.TenantId,scope.EstablishmentId,"PIX","Pix",true,false,DateTimeOffset.UtcNow);
        var orders=new OrderRepository();
        var unavailable=new AvailabilityGateway(new(false,AvailabilityReason.ServicePaused,"Pickup paused.",DateTimeOffset.UtcNow.AddHours(1)));
        var handler=new ConfirmPublicOrderCommandHandler(new ContextGateway(scope),new OfferResolver(20m),new CustomerResolver(),new TableResolver(),new CouponRepository(),new ActivePaymentMethodRepository(method),new PaymentRepository(),orders,new Sequence(),new RequestRepository(),new Transaction(),unavailable,new DeliveryGateway(),new SchedulingRepository(),new CustomerNotificationStager(),new Publisher(),TimeProvider.System);
        var command=new ConfirmPublicOrderCommand("unit-a","stale-cart-key-123",OrderServiceType.Pickup,null,null,null,null,null,method.Id,null,[new(Guid.NewGuid(),null,1,null,[])]);

        var exception=await Assert.ThrowsAsync<AvailabilityConflictException>(()=>handler.HandleAsync(command,CancellationToken.None));

        Assert.Equal(AvailabilityReason.ServicePaused,exception.Reason);
        Assert.Equal(0,orders.AddCount);
    }

    private static PublicOrderingContext Scope()=>new(Guid.NewGuid(),Guid.NewGuid(),"Unit","unit-a","#1","#2","#3","#4","Arial",null,null,null,null,[]);
    private sealed class CustomerNotificationStager : ICustomerOrderNotificationStager { public int Calls { get; private set; } public Task StageAsync(Order order, CancellationToken cancellationToken) { Calls++; return Task.CompletedTask; } }
    private sealed class ContextGateway(PublicOrderingContext? value):IPublicOrderingContextGateway { public Task<PublicOrderingContext?> ResolveAsync(string normalizedSlug,string? tableToken,CancellationToken cancellationToken)=>Task.FromResult(value); }
    private sealed class OfferResolver(decimal price):IOrderOfferResolver { public Task<OrderOfferSnapshot?> ResolveAsync(Guid tenantId,Guid establishmentId,Guid productId,Guid? variationId,IReadOnlyCollection<OrderAdditionalSelection> additionals,DateTimeOffset instant,CancellationToken cancellationToken)=>Task.FromResult<OrderOfferSnapshot?>(new(productId,variationId,"Pizza",null,new Money(price),[])); }
    private sealed class CustomerResolver:IOrderCustomerResolver { public Task<OrderCustomerSnapshot?> ResolveAsync(Guid tenantId,Guid establishmentId,Guid customerId,Guid? addressId,CancellationToken cancellationToken)=>Task.FromResult<OrderCustomerSnapshot?>(null); }
    private sealed class TableResolver:IOrderTableResolver { public Task<OrderTableSnapshot?> ResolveActiveAsync(Guid tenantId,Guid establishmentId,Guid tableId,CancellationToken cancellationToken)=>Task.FromResult<OrderTableSnapshot?>(null); }
    private sealed class CouponRepository:ICouponRepository { public Task<Coupon?> GetAsync(Guid a,Guid b,Guid c,CancellationToken d)=>Task.FromResult<Coupon?>(null); public Task<Coupon?> FindByCodeAsync(Guid a,Guid b,string c,CancellationToken d)=>Task.FromResult<Coupon?>(null); public Task AddAsync(Coupon a,CancellationToken b)=>Task.CompletedTask; public Task SaveChangesAsync(CancellationToken a)=>Task.CompletedTask; }
    private sealed class PaymentMethodRepository:IPaymentMethodRepository { public Task<PaymentMethod?> GetAsync(Guid a,Guid b,Guid c,CancellationToken d)=>Task.FromResult<PaymentMethod?>(null); public Task<bool> CodeExistsAsync(Guid a,Guid b,string c,Guid? d,CancellationToken e)=>Task.FromResult(false); public Task AddAsync(PaymentMethod a,CancellationToken b)=>Task.CompletedTask; public Task SaveChangesAsync(CancellationToken a)=>Task.CompletedTask; }
    private sealed class ActivePaymentMethodRepository(PaymentMethod method):IPaymentMethodRepository { public Task<PaymentMethod?> GetAsync(Guid a,Guid b,Guid c,CancellationToken d)=>Task.FromResult<PaymentMethod?>(c==method.Id?method:null); public Task<bool> CodeExistsAsync(Guid a,Guid b,string c,Guid? d,CancellationToken e)=>Task.FromResult(false); public Task AddAsync(PaymentMethod a,CancellationToken b)=>Task.CompletedTask; public Task SaveChangesAsync(CancellationToken a)=>Task.CompletedTask; }
    private sealed class PaymentRepository:IPaymentRepository { public Task<Payment?> GetAsync(Guid a,Guid b,Guid c,CancellationToken d)=>Task.FromResult<Payment?>(null); public Task AddAsync(Payment a,CancellationToken b)=>Task.CompletedTask; public Task SaveChangesAsync(CancellationToken a)=>Task.CompletedTask; }
    private sealed class OrderRepository:IOrderRepository { private readonly Dictionary<Guid,Order> values=[]; public int AddCount{get;private set;} public Order? LastOrder => values.Values.SingleOrDefault(); public Task<Order?> GetAsync(Guid a,Guid b,Guid id,CancellationToken d)=>Task.FromResult(values.GetValueOrDefault(id)); public Task AddAsync(Order order,CancellationToken b){values.Add(order.Id,order);AddCount++;return Task.CompletedTask;} public Task SaveChangesAsync(CancellationToken a)=>Task.CompletedTask; }
    private sealed class Sequence:IOrderNumberSequence { private long number; public Task<long> ReserveAsync(Guid a,Guid b,CancellationToken c)=>Task.FromResult(++number); }
    private sealed class RequestRepository:IPublicOrderRequestRepository { private PublicOrderRequest? value; public Task<PublicOrderRequest?> FindAsync(Guid a,Guid b,string c,CancellationToken d)=>Task.FromResult(value?.Key==c?value:null); public Task AddAsync(PublicOrderRequest request,CancellationToken b){value=request;return Task.CompletedTask;} }
    private sealed class Transaction:IPublicOrderTransaction { public Task<T> ExecuteAsync<T>(Func<CancellationToken,Task<T>> operation,CancellationToken token)=>operation(token); }
    private sealed class AvailabilityGateway(AvailabilityDecision? value=null) : IAvailabilityReadGateway, IOrderAvailabilityGateway { public Task<AvailabilityDecision> EvaluateAsync(Guid tenantId,Guid establishmentId,OrderServiceType serviceType,DateTimeOffset instant,CancellationToken cancellationToken)=>Task.FromResult(value??AvailabilityDecision.Available()); }
    private sealed class AvailabilityClosedUntilFutureGateway : IAvailabilityReadGateway, IOrderAvailabilityGateway
    {
        public Task<AvailabilityDecision> EvaluateAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, DateTimeOffset instant, CancellationToken cancellationToken) =>
            Task.FromResult(instant > DateTimeOffset.UtcNow.AddMinutes(60)
                ? AvailabilityDecision.Available()
                : new AvailabilityDecision(false, AvailabilityReason.OutsideBusinessHours, "Closed now.", DateTimeOffset.UtcNow.AddHours(2)));
    }
    private sealed class Publisher : IOrderUpdatePublisher { public Task PublishAsync(Guid tenantId, OrderUpdateSignal signal, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class SchedulingRepository(OrderSchedulingPolicy? policy = null, string? timeZoneId = null) : IOrderSchedulingRepository
    {
        public Task<OrderSchedulingPolicy?> GetPolicyAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, CancellationToken cancellationToken) => Task.FromResult(policy?.ServiceType == serviceType ? policy : null);
        public Task<IReadOnlyList<OrderSchedulingPolicy>> GetPoliciesAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderSchedulingPolicy>>([]);
        public Task<string?> GetTimeZoneIdAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken) => Task.FromResult(timeZoneId);
        public Task<int> CountReservedOrdersAsync(Guid tenantId, Guid establishmentId, OrderServiceType serviceType, DateTimeOffset slotStart, CancellationToken cancellationToken) => Task.FromResult(0);
        public void Add(OrderSchedulingPolicy policy) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class DeliveryGateway(DeliveryQuote? quote = null) : IDeliveryReadGateway
    {
        public Task<IReadOnlyList<DeliveryRegionReadModel>> ListAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DeliveryRegionReadModel>>([]);
        public Task<DeliveryQuote?> FindQuoteAsync(Guid tenantId, Guid establishmentId, string normalizedPostalCode, CancellationToken cancellationToken) => Task.FromResult(quote);
    }
}
