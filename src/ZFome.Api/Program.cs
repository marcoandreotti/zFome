using ZFome.Api.Middleware;
using ZFome.Api.Tenancy;
using ZFome.Application;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ZFome.Application.Identity;
using ZFome.Api.Catalog;
using ZFome.Api.PublicOrdering;
using ZFome.Api.Administration;
using ZFome.Api.Authentication;
using ZFome.Api.Platform;
using Microsoft.AspNetCore.Authentication;
using ZFome.Infrastructure.Identity;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ZFome.Api.Realtime;
using ZFome.Application.Abstractions.Ordering;

var builder = WebApplication.CreateBuilder(args);

// Mensagens padrão dos validators seguem o idioma da interface.
FluentValidation.ValidatorOptions.Global.LanguageManager.Culture =
    System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPlatformProvisioning();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddSingleton<OrderUpdateSubscriptions>();
builder.Services.AddSingleton<OrderRealtimeTelemetry>();
builder.Services.AddScoped<IOrderUpdatePublisher, SignalROrderUpdatePublisher>();
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("RealtimeOrders:ClientTimeoutSeconds", 30));
    options.KeepAliveInterval = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("RealtimeOrders:KeepAliveSeconds", 15));
    options.MaximumReceiveMessageSize = builder.Configuration.GetValue<long?>(
        "RealtimeOrders:MaximumReceiveMessageBytes") ?? 32 * 1024;
});
var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = builder.Environment.IsEnvironment("Testing") ? ExistingPrincipalAuthenticationHandler.SchemeName : SessionAuthenticationHandler.SchemeName;
    options.DefaultChallengeScheme = options.DefaultAuthenticateScheme;
});
if (builder.Environment.IsEnvironment("Testing")) authentication.AddScheme<AuthenticationSchemeOptions, ExistingPrincipalAuthenticationHandler>(ExistingPrincipalAuthenticationHandler.SchemeName, _ => { });
else authentication.AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddProblemDetails();
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("authentication-attempts", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("public-review-submissions", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new { title = "Muitas solicitações. Tente novamente em instantes.", status = 429 }, cancellationToken);
    };
});
builder.Services.AddAuthorization(options =>
{
    foreach (var (policyName, roles) in AdministrativePolicies.RoleMap)
    {
        options.AddPolicy(policyName, policy => policy.RequireAssertion(context => context.User.HasClaim("platform_user", "true") || roles.Any(role => context.User.IsInRole(role.ToString()))));
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "ZFome API v1"));
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("web");
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<AuthenticationSecurityMiddleware>();
app.UseAuthorization();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapCatalogEndpoints();
app.MapPublicOrderingEndpoints();
app.MapReviewEndpoints();
app.MapAdministrationEndpoints();
app.MapOnboardingEndpoints();
app.MapAdministrativeUserEndpoints();
app.MapAuthenticationEndpoints();
app.MapPlatformProvisioningEndpoints();
app.MapHub<OrderUpdatesHub>("/hubs/order-updates")
    .RequireAuthorization(AdministrativePolicies.OrderRead);

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapGet("/_test/tenant", (ITenantContext tenantContext) => Results.Ok(new
    {
        tenantId = tenantContext.GetRequiredTenantId()
    }));
}
else if (app.Environment.IsProduction() ||
         (app.Environment.IsDevelopment() &&
          builder.Configuration.GetValue<bool>($"{PlatformBootstrapOptions.SectionName}:Enabled")))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PlatformBootstrapper>().InitializeAsync(CancellationToken.None);
}

app.Run();

public partial class Program;
