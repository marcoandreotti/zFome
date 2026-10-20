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
using OrderHub.Application.Abstractions.Queries;
using OrderHub.Application.Abstractions.Reviews;
using OrderHub.Application.Abstractions.Tenancy;
using OrderHub.Application.Reviews;
using OrderHub.Contracts.Reviews;
using OrderHub.Domain.Reviews;

namespace OrderHub.Integration.Tests;

public sealed class ReviewApiTests
{
    private static readonly Guid UnitId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Administrative_list_serializes_review_status_as_contract_name()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/admin/establishments/{UnitId}/reviews");
        var payload = await response.Content.ReadFromJsonAsync<ReviewAdminPageResponse>();
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Pending", Assert.Single(payload!.Items).Status);
        Assert.Contains("\"status\":\"Pending\"", json);
        Assert.DoesNotContain("\"status\":0", json);
    }

    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.RemoveAll<IQueryDispatcher>();
                services.RemoveAll<IEstablishmentAccessGateway>();
                services.AddSingleton<IQueryDispatcher, Dispatcher>();
                services.AddSingleton<IEstablishmentAccessGateway, AccessGateway>();
            });
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ReviewTests";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("tenant_id", Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Manager")
            ], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class AccessGateway : IEstablishmentAccessGateway
    {
        public Task<bool> HasActiveAccessAsync(Guid tenantId, Guid userId, Guid establishmentId,
            CancellationToken cancellationToken) => Task.FromResult(establishmentId == UnitId);
    }

    private sealed class Dispatcher : IQueryDispatcher
    {
        public Task<TResult> DispatchAsync<TQuery, TResult>(TQuery query,
            CancellationToken cancellationToken = default) where TQuery : IQuery<TResult>
        {
            var now = new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero);
            object result = query switch
            {
                ListAdminReviewsQuery => new ReviewAdminPage(1,
                    [new ReviewAdminItem(Guid.NewGuid(), null, null, 5, null,
                        ReviewStatus.Pending, null, now, now)]),
                _ => throw new NotSupportedException(query.GetType().Name)
            };
            return Task.FromResult((TResult)result);
        }
    }
}
