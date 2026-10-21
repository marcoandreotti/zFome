using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ZFome.Application.Abstractions.Catalog;
using ZFome.Contracts.Catalog;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Exceptions;
using FluentValidation.Results;

namespace ZFome.Integration.Tests;

public sealed class CatalogApiTests
{
    [Fact]
    public async Task Public_catalog_returns_external_contract_without_tenant_id()
    {
        var model = new CatalogReadModel(Guid.NewGuid(), "Unit", "unit-a", [new(Guid.NewGuid(), null, "Pizzas", null, 0, null, true, [])]);
        await using var factory = new CatalogApiFactory(model); using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/public/establishments/unit-a/catalog"); var payload = await response.Content.ReadFromJsonAsync<CatalogResponse>(); var json = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("unit-a", payload!.Slug); Assert.DoesNotContain("tenantId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_public_catalog_uses_standard_problem_details()
    {
        await using var factory = new CatalogApiFactory(null); using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/public/establishments/missing/catalog"); var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.Equal("Resource not found", problem!.Title); Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Administrative_catalog_requires_authentication()
    {
        await using var factory = new CatalogApiFactory(new CatalogReadModel(Guid.NewGuid(), "Unit", "unit-a", [])); using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/admin/establishments/{Guid.NewGuid()}/catalog/");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_dispatch_administrative_catalog_command()
    {
        await using var factory = new AuthenticatedCatalogApiFactory(new CommandDispatcher(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))); using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/api/admin/establishments/{Guid.NewGuid()}/catalog/categories", new UpsertCategoryRequest("Pizzas", null, 0, null, null, true));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("validation", HttpStatusCode.BadRequest)]
    [InlineData("conflict", HttpStatusCode.Conflict)]
    [InlineData("forbidden", HttpStatusCode.Forbidden)]
    public async Task Administrative_errors_are_problem_details(string error, HttpStatusCode status)
    {
        await using var factory = new AuthenticatedCatalogApiFactory(new CommandDispatcher(error)); using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync($"/api/admin/establishments/{Guid.NewGuid()}/catalog/categories", new UpsertCategoryRequest("Pizzas", null, 0, null, null, true));
        Assert.Equal(status, response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private sealed class CatalogApiFactory(CatalogReadModel? model) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services => { services.RemoveAll<ICatalogReadGateway>(); services.AddSingleton<ICatalogReadGateway>(new ReadGateway(model)); });
        }
    }
    private sealed class ReadGateway(CatalogReadModel? model) : ICatalogReadGateway
    {
        public Task<CatalogReadModel?> GetAdministrativeAsync(Guid tenantId, Guid establishmentId, CancellationToken cancellationToken) => Task.FromResult(model);
        public Task<CatalogReadModel?> GetPublicAsync(string normalizedSlug, CancellationToken cancellationToken) => Task.FromResult(model);
    }
    private sealed class AuthenticatedCatalogApiFactory(ICommandDispatcher dispatcher) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing"); builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options => { options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName; options.DefaultChallengeScheme = TestAuthHandler.SchemeName; }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.RemoveAll<ICommandDispatcher>(); services.AddSingleton(dispatcher);
            });
        }
    }
    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "CatalogTest";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim("sub", Guid.NewGuid().ToString()), new Claim("tenant_id", Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "Manager")], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
    private sealed class CommandDispatcher : ICommandDispatcher
    {
        private readonly Guid? result; private readonly string? error;
        public CommandDispatcher(Guid result) => this.result = result; public CommandDispatcher(string error) => this.error = error;
        public Task DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand => throw new NotSupportedException();
        public Task<TResult> DispatchAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand<TResult>
        {
            if (error == "validation") throw new ValidationException([new ValidationFailure("Name", "Invalid")]);
            if (error == "conflict") throw new ConflictException("Conflict");
            if (error == "forbidden") throw new ForbiddenException("Forbidden");
            return Task.FromResult((TResult)(object)result!.Value);
        }
    }
}
