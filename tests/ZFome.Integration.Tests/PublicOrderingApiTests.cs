using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Ordering;
using ZFome.Application.Abstractions.PublicOrdering;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.PublicOrdering;
using ZFome.Contracts.PublicOrdering;
using ZFome.Domain.Ordering;

namespace ZFome.Integration.Tests;

public sealed class PublicOrderingApiTests
{
    [Fact]
    public async Task Context_exposes_only_public_active_data()
    {
        var dispatcher=new PublicDispatcher(); await using var factory=new PublicApiFactory(dispatcher); using var client=factory.CreateClient();
        var response=await client.GetAsync("/api/public/ordering/unit-a/context"); var json=await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK,response.StatusCode); Assert.DoesNotContain("tenantId",json,StringComparison.OrdinalIgnoreCase); Assert.Contains("PIX",json);
    }

    [Fact]
    public async Task Confirmation_requires_idempotency_header_as_problem_details()
    {
        await using var factory=new PublicApiFactory(null); using var client=factory.CreateClient();
        var request=new PublicOrderConfirmationRequest("Pickup",null,null,null,null,null,Guid.NewGuid(),null,[new(Guid.NewGuid(),null,1,null,[])]);
        var response=await client.PostAsJsonAsync("/api/public/ordering/unit-a/orders",request); var problem=await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode); Assert.Equal("Falha na validação",problem!.Title); Assert.Equal("application/problem+json",response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Invalid_public_reference_returns_problem_details_without_lookup()
    {
        await using var factory=new PublicApiFactory(null); using var client=factory.CreateClient();
        var response=await client.GetAsync("/api/public/ordering/orders/123");
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode); Assert.Equal("application/problem+json",response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Confirmation_contract_has_no_client_price_or_tenant_authority()
    {
        var dispatcher=new PublicDispatcher(); await using var factory=new PublicApiFactory(dispatcher); using var client=factory.CreateClient();
        using var message=new HttpRequestMessage(HttpMethod.Post,"/api/public/ordering/unit-a/orders"){Content=JsonContent.Create(new PublicOrderConfirmationRequest("Pickup",null,null,null,null,null,Guid.NewGuid(),null,[new(Guid.NewGuid(),null,2,null,[])]))}; message.Headers.Add("Idempotency-Key","abcdefgh-12345678");
        var response=await client.SendAsync(message); var json=await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created,response.StatusCode); Assert.DoesNotContain("tenantId",json,StringComparison.OrdinalIgnoreCase); Assert.Equal(2,dispatcher.LastConfirmation!.Items.Single().Quantity);
    }

    [Fact]
    public async Task OpenApi_contains_public_ordering_routes_and_contracts()
    {
        await using var factory=new OpenApiFactory(); using var client=factory.CreateClient();
        var json=await client.GetStringAsync("/openapi/v1.json");
        Assert.Contains("/api/public/ordering/{slug}/context",json); Assert.Contains("/api/public/ordering/{slug}/orders",json); Assert.Contains(nameof(PublicOrderConfirmationRequest),json);
    }

    private sealed class PublicApiFactory(object? dispatcher):WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder){builder.UseEnvironment("Testing");builder.ConfigureLogging(x=>x.ClearProviders());builder.ConfigureTestServices(services=>{if(dispatcher is null)return;services.RemoveAll<ICommandDispatcher>();services.RemoveAll<IQueryDispatcher>();services.AddSingleton((ICommandDispatcher)dispatcher);services.AddSingleton((IQueryDispatcher)dispatcher);});}
    }
    private sealed class OpenApiFactory:WebApplicationFactory<Program> { protected override void ConfigureWebHost(IWebHostBuilder builder){builder.UseEnvironment("Development");builder.ConfigureLogging(x=>x.ClearProviders());} }

    private sealed class PublicDispatcher:ICommandDispatcher,IQueryDispatcher
    {
        public ConfirmPublicOrderCommand? LastConfirmation {get;private set;}
        public Task DispatchAsync<TCommand>(TCommand command,CancellationToken cancellationToken=default) where TCommand:ICommand=>Task.CompletedTask;
        public Task<TResult> DispatchAsync<TCommand,TResult>(TCommand command,CancellationToken cancellationToken=default) where TCommand:ICommand<TResult>
        { if(command is ConfirmPublicOrderCommand confirmation){LastConfirmation=confirmation;return Task.FromResult((TResult)(object)new PublicConfirmation(new string('a',48),42,OrderStatus.Confirmed,50m));} throw new NotSupportedException(); }
        Task<TResult> IQueryDispatcher.DispatchAsync<TQuery,TResult>(TQuery query,CancellationToken cancellationToken)
        { if(query is GetPublicContextQuery)return Task.FromResult((TResult)(object)new PublicOrderingContext(Guid.NewGuid(),Guid.NewGuid(),"Unit","unit-a","#1","#2","#3","#4","Arial",null,null,null,null,[new(Guid.NewGuid(),"PIX","Pix",true,false)])); throw new NotSupportedException(); }
    }
}
