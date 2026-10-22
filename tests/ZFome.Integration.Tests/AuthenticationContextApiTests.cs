using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Identity.Authentication;
using ZFome.Contracts.Authentication;
using ZFome.Domain.Identity;

namespace ZFome.Integration.Tests;

public sealed class AuthenticationContextApiTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("expired")]
    public async Task Missing_or_expired_session_is_rejected(string? token)
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Add("Cookie", $"zf_access={token}");
        using var response = await client.GetAsync("/api/auth/context");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Restricted_session_can_read_context_but_not_operational_data()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "zf_access=restricted");
        using var response = await client.GetAsync("/api/auth/context");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthenticationContextResponse>();
        Assert.True(result!.PasswordChangeRequired);
        Assert.Empty(result.Capabilities);
        Assert.Empty(result.Establishments);
        Assert.True(response.Headers.CacheControl!.NoStore);
        using var forbidden = await client.GetAsync($"/api/admin/establishments/{Guid.NewGuid()}/customers");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Context_exposes_server_capabilities_without_tokens_or_internal_claims()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "zf_access=active");
        using var response = await client.GetAsync("/api/auth/context");
        var result = await response.Content.ReadFromJsonAsync<AuthenticationContextResponse>();
        Assert.Contains("management", result!.Capabilities);
        Assert.DoesNotContain("administration", result.Capabilities);
        Assert.Single(result.Establishments);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sessionId", json);
        Assert.DoesNotContain("accessToken", json);
        Assert.DoesNotContain("tenantId", json);
    }

    [Fact]
    public async Task Invalid_login_attempts_are_rate_limited_before_database_access()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();
        for (var index = 0; index < 20; index++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/begin", new BeginAuthenticationRequest("", "", ""));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var limited = await client.PostAsJsonAsync("/api/auth/begin", new BeginAuthenticationRequest("", "", ""));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Cors_preflight_allows_only_the_configured_web_origin_with_credentials()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();
        using var allowedRequest = Preflight("http://localhost:9000");
        using var allowedResponse = await client.SendAsync(allowedRequest);

        Assert.Equal(HttpStatusCode.NoContent, allowedResponse.StatusCode);
        Assert.Equal("http://localhost:9000", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", allowedResponse.Headers.GetValues("Access-Control-Allow-Credentials").Single());

        using var deniedRequest = Preflight("http://untrusted.example");
        using var deniedResponse = await client.SendAsync(deniedRequest);
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/begin");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type,x-correlation-id,x-csrf-token");
        return request;
    }

    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:9000");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuthenticationSessionResolver>();
                services.RemoveAll<IAuthenticationContextGateway>();
                services.AddSingleton<IAuthenticationSessionResolver, Resolver>();
                services.AddSingleton<IAuthenticationContextGateway, Gateway>();
            });
        }
    }

    private sealed class Resolver : IAuthenticationSessionResolver
    {
        public Task<AuthenticatedIdentity?> ResolveAsync(string token, CancellationToken ct) => Task.FromResult<AuthenticatedIdentity?>(token switch
        {
            "restricted" => new(Guid.NewGuid(), AuthenticationIdentityType.PlatformUser, Guid.NewGuid(), null, [], [], true),
            "active" => new(Guid.NewGuid(), AuthenticationIdentityType.AdministrativeUser, Guid.NewGuid(), Guid.NewGuid(), [AdministrativeRole.Manager], [], false),
            _ => null
        });
    }

    private sealed class Gateway : IAuthenticationContextGateway
    {
        public Task<IReadOnlyCollection<AuthenticationEstablishment>> GetEstablishmentsAsync(AuthenticatedIdentity identity, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<AuthenticationEstablishment>>([new(Guid.NewGuid(), "Unidade autorizada")]);
    }
}
