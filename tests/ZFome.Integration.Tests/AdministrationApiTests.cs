using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Customers;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Customers;
using ZFome.Application.Exceptions;
using ZFome.Application.Ordering;
using ZFome.Application.Promotions;
using ZFome.Contracts.Administration;
using ZFome.Domain.Identity;

namespace ZFome.Integration.Tests;

public sealed class AdministrationApiTests
{
    private static readonly Guid UnitId=Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Theory]
    [InlineData(AdministrativeRole.Owner,true)] [InlineData(AdministrativeRole.Admin,true)] [InlineData(AdministrativeRole.Manager,true)]
    [InlineData(AdministrativeRole.Attendant,false)] [InlineData(AdministrativeRole.Kitchen,false)] [InlineData(AdministrativeRole.Delivery,false)]
    public async Task Coupon_management_obeys_role_matrix(AdministrativeRole role,bool allowed)
    {
        await using var factory=new AdminFactory();using var client=factory.CreateClient();client.DefaultRequestHeaders.Add("X-Test-Role",role.ToString());
        var response=await client.GetAsync($"/api/admin/establishments/{UnitId}/coupons"); Assert.Equal(allowed?HttpStatusCode.OK:HttpStatusCode.Forbidden,response.StatusCode);
    }

    [Theory]
    [InlineData(AdministrativeRole.Owner,true)] [InlineData(AdministrativeRole.Admin,true)] [InlineData(AdministrativeRole.Manager,true)]
    [InlineData(AdministrativeRole.Attendant,false)] [InlineData(AdministrativeRole.Kitchen,false)] [InlineData(AdministrativeRole.Delivery,false)]
    public async Task Order_scheduling_configuration_obeys_management_role_matrix(AdministrativeRole role, bool allowed)
    {
        await using var factory = new AdminFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", role.ToString());
        var response = await client.PutAsJsonAsync($"/api/admin/establishments/{UnitId}/order-scheduling/Pickup",
            new OrderSchedulingPolicyRequest(true, 60, 30, null));
        Assert.Equal(allowed ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(AdministrativeRole.Owner,true)] [InlineData(AdministrativeRole.Admin,true)] [InlineData(AdministrativeRole.Manager,true)] [InlineData(AdministrativeRole.Kitchen,true)]
    [InlineData(AdministrativeRole.Attendant,false)] [InlineData(AdministrativeRole.Delivery,false)]
    public async Task Kitchen_transition_obeys_role_matrix(AdministrativeRole role,bool allowed)
    {
        await using var factory=new AdminFactory();using var client=factory.CreateClient();client.DefaultRequestHeaders.Add("X-Test-Role",role.ToString());
        var response=await client.PostAsJsonAsync($"/api/admin/establishments/{UnitId}/orders/{Guid.NewGuid()}/prepare",new OrderTransitionRequest(null));Assert.Equal(allowed?HttpStatusCode.NoContent:HttpStatusCode.Forbidden,response.StatusCode);
    }

    [Theory]
    [InlineData(AdministrativeRole.Owner,true)] [InlineData(AdministrativeRole.Admin,true)] [InlineData(AdministrativeRole.Manager,true)] [InlineData(AdministrativeRole.Kitchen,true)]
    [InlineData(AdministrativeRole.Attendant,false)] [InlineData(AdministrativeRole.Delivery,false)]
    public async Task Kitchen_queue_obeys_role_matrix(AdministrativeRole role,bool allowed)
    {
        await using var factory=new AdminFactory();using var client=factory.CreateClient();client.DefaultRequestHeaders.Add("X-Test-Role",role.ToString());
        var response=await client.GetAsync($"/api/admin/establishments/{UnitId}/kitchen");Assert.Equal(allowed?HttpStatusCode.OK:HttpStatusCode.Forbidden,response.StatusCode);
    }

    [Fact]
    public async Task Stale_kitchen_transition_is_returned_as_problem_details_conflict()
    {
        await using var factory=new AdminFactory(conflict:true);using var client=factory.CreateClient();client.DefaultRequestHeaders.Add("X-Test-Role",AdministrativeRole.Kitchen.ToString());
        var response=await client.PostAsJsonAsync($"/api/admin/establishments/{UnitId}/orders/{Guid.NewGuid()}/prepare",new OrderTransitionRequest(null));
        Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);Assert.Equal("application/problem+json",response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(AdministrativeRole.Owner,true)] [InlineData(AdministrativeRole.Admin,true)] [InlineData(AdministrativeRole.Manager,true)] [InlineData(AdministrativeRole.Delivery,true)]
    [InlineData(AdministrativeRole.Attendant,false)] [InlineData(AdministrativeRole.Kitchen,false)]
    public async Task Delivery_transition_obeys_role_matrix(AdministrativeRole role,bool allowed)
    {
        await using var factory=new AdminFactory();using var client=factory.CreateClient();client.DefaultRequestHeaders.Add("X-Test-Role",role.ToString());
        var response=await client.PostAsJsonAsync($"/api/admin/establishments/{UnitId}/orders/{Guid.NewGuid()}/dispatch",new OrderTransitionRequest(null));Assert.Equal(allowed?HttpStatusCode.NoContent:HttpStatusCode.Forbidden,response.StatusCode);
    }

    [Theory]
    [InlineData(AdministrativeRole.Owner,true)] [InlineData(AdministrativeRole.Admin,true)] [InlineData(AdministrativeRole.Manager,true)] [InlineData(AdministrativeRole.Attendant,true)]
    [InlineData(AdministrativeRole.Kitchen,false)] [InlineData(AdministrativeRole.Delivery,false)]
    public async Task Customer_operations_obey_role_matrix(AdministrativeRole role,bool allowed)
    {
        await using var factory=new AdminFactory();using var client=factory.CreateClient();client.DefaultRequestHeaders.Add("X-Test-Role",role.ToString());
        var response=await client.GetAsync($"/api/admin/establishments/{UnitId}/customers");Assert.Equal(allowed?HttpStatusCode.OK:HttpStatusCode.Forbidden,response.StatusCode);
    }

    [Fact]
    public async Task Cross_unit_scope_is_forbidden_as_problem_details()
    {
        await using var factory=new AdminFactory(true);using var client=factory.CreateClient();client.DefaultRequestHeaders.Add("X-Test-Role",AdministrativeRole.Manager.ToString());
        var response=await client.GetAsync($"/api/admin/establishments/{Guid.NewGuid()}/customers");
        Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);Assert.Equal("application/problem+json",response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Administrative_routes_and_explicit_contracts_are_in_openapi()
    {
        await using var factory=new AdminFactory(environment:"Development");using var client=factory.CreateClient();var json=await client.GetStringAsync("/openapi/v1.json");
        Assert.Contains("/api/admin/establishments/{establishmentId}/customers",json);Assert.Contains("/api/admin/establishments/{establishmentId}/orders",json);Assert.Contains("/api/admin/establishments/{establishmentId}/kitchen",json);Assert.Contains("/api/admin/establishments/{establishmentId}/order-scheduling",json);Assert.Contains(nameof(PagedResponse<CustomerResponse>),json);Assert.Contains(nameof(KitchenTicketResponse),json);
    }

    private sealed class AdminFactory(bool rejectScope=false,string environment="Testing",bool conflict=false):WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder){builder.UseEnvironment(environment);builder.ConfigureLogging(x=>x.ClearProviders());builder.ConfigureTestServices(services=>{services.AddAuthentication(o=>{o.DefaultAuthenticateScheme=TestAuthHandler.SchemeName;o.DefaultChallengeScheme=TestAuthHandler.SchemeName;}).AddScheme<AuthenticationSchemeOptions,TestAuthHandler>(TestAuthHandler.SchemeName,_=>{});services.RemoveAll<ICommandDispatcher>();services.RemoveAll<IQueryDispatcher>();var dispatcher=new Dispatcher(rejectScope,conflict);services.AddSingleton<ICommandDispatcher>(dispatcher);services.AddSingleton<IQueryDispatcher>(dispatcher);});}
    }

    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        public const string SchemeName="AdministrationTests";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync(){var role=Request.Headers["X-Test-Role"].ToString();if(string.IsNullOrWhiteSpace(role))return Task.FromResult(AuthenticateResult.NoResult());var identity=new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim("sub",Guid.NewGuid().ToString()),new Claim("tenant_id",Guid.NewGuid().ToString()),new Claim(ClaimTypes.Role,role)],SchemeName);return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity),SchemeName)));}
    }

    private sealed class Dispatcher(bool rejectScope,bool conflict):ICommandDispatcher,IQueryDispatcher
    {
        public Task DispatchAsync<TCommand>(TCommand command,CancellationToken cancellationToken=default) where TCommand:ICommand=>conflict?throw new ConflictException("Order was changed by another operation."):Task.CompletedTask;
        public Task<TResult> DispatchAsync<TCommand,TResult>(TCommand command,CancellationToken cancellationToken=default) where TCommand:ICommand<TResult> => Task.FromResult((TResult)(object)Guid.NewGuid());
        Task<TResult> IQueryDispatcher.DispatchAsync<TQuery,TResult>(TQuery query,CancellationToken cancellationToken)
        {if(rejectScope)throw new ForbiddenException("Active access to the establishment is required.");object result=query switch{SearchCustomersQuery _=>new CustomerSearchResult(0,[]),SearchCouponsQuery _=>new ZFome.Application.Abstractions.Promotions.CouponSearchResult(0,[]),GetKitchenQueueQuery _=>Array.Empty<KitchenTicketReadModel>(),_=>throw new NotSupportedException(query.GetType().Name)};return Task.FromResult((TResult)result);}
    }
}
