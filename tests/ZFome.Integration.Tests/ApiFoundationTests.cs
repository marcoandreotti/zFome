using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ZFome.Integration.Tests;

public sealed class ApiFoundationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;

    public ApiFoundationTests(ApiFactory factory) => client = factory.CreateClient();

    [Fact]
    public async Task Health_endpoint_is_available()
    {
        using var response = await client.GetAsync("/health", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Correlation_id_is_preserved_in_response()
    {
        const string correlationId = "integration-test-correlation";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-ID", correlationId);

        using var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Missing_tenant_fails_safely_with_problem_details()
    {
        using var response = await client.GetAsync("/_test/tenant", CancellationToken.None);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Acesso negado", problem?.Title);
        Assert.Equal("Você não tem permissão para realizar esta operação.", problem?.Detail);
        Assert.True(problem?.Extensions.ContainsKey("traceId"));
    }

    [Fact]
    public async Task Public_validation_errors_use_portuguese_messages_in_problem_details()
    {
        using var response = await client.PostAsJsonAsync(
            "/api/public/ordering/unit/simulate",
            new
            {
                ServiceType = "Pickup",
                Items = Array.Empty<object>()
            },
            CancellationToken.None);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        var root = document.RootElement;
        var errors = root.GetProperty("errors").EnumerateObject()
            .SelectMany(property => property.Value.EnumerateArray())
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Falha na validação", root.GetProperty("title").GetString());
        Assert.NotEmpty(errors);
        Assert.Contains("'Items' deve ser informado.", errors);
        Assert.True(root.TryGetProperty("traceId", out _));
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
}

public sealed class OpenApiExposureTests
{
    [Fact]
    public async Task OpenApi_and_Swagger_UI_are_available_in_development()
    {
        await using var factory = new ApiEnvironmentFactory("Development");
        using var client = factory.CreateClient();

        using var documentResponse = await client.GetAsync("/openapi/v1.json", CancellationToken.None);
        using var uiResponse = await client.GetAsync("/swagger/index.html", CancellationToken.None);
        var document = await documentResponse.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, uiResponse.StatusCode);
        Assert.Contains("/api/auth/begin", document);
        Assert.Contains("/api/auth/complete", document);
        Assert.Contains(nameof(ZFome.Contracts.Authentication.BeginAuthenticationRequest), document);
    }

    [Fact]
    public async Task OpenApi_and_Swagger_UI_are_not_available_outside_development()
    {
        await using var factory = new ApiEnvironmentFactory("Testing");
        using var client = factory.CreateClient();

        using var documentResponse = await client.GetAsync("/openapi/v1.json", CancellationToken.None);
        using var uiResponse = await client.GetAsync("/swagger/index.html", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, documentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, uiResponse.StatusCode);
    }

    private sealed class ApiEnvironmentFactory(string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment(environment);
    }
}
