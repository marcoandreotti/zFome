using ZFome.Api.Authentication;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Platform;
using ZFome.Contracts.Platform;

namespace ZFome.Api.Platform;

internal static class PlatformProvisioningEndpoints
{
    public static IEndpointRouteBuilder MapPlatformProvisioningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/platform/tenants")
            .WithTags("Platform - Tenants")
            .RequireAuthorization(policy => policy.RequireClaim("platform_user", "true"));
        group.MapGet("", SearchAsync).Produces<PlatformTenantPageResponse>();
        group.MapGet("/{tenantId:guid}", GetAsync).Produces<PlatformTenantResponse>();
        group.MapGet("/provisioning/{intentKey:guid}", GetIntentAsync).Produces<PlatformProvisioningResponse>();
        group.MapPost("", ProvisionAsync).Produces<PlatformProvisioningResponse>();
        group.MapPost("/{tenantId:guid}/establishments", AddEstablishmentAsync).Produces<PlatformProvisioningResponse>();
        return endpoints;
    }

    private static string AccessToken(HttpContext context) =>
        context.Request.Cookies.TryGetValue(AuthenticationEndpoints.AccessCookie, out var token) ? token : string.Empty;

    private static async Task<IResult> ProvisionAsync(ProvisionTenantRequest request, HttpContext context,
        ICommandDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.DispatchAsync<ProvisionTenantCommand, PlatformProvisioningResult>(new(
            AccessToken(context), request.IntentKey, request.TenantName, request.TenantPublicCode,
            request.EstablishmentName, request.EstablishmentSlug, request.TimeZoneId, request.OwnerName,
            request.OwnerEmail, request.TemporaryPassword), ct);
        return Results.Ok(Map(result));
    }

    private static async Task<IResult> AddEstablishmentAsync(Guid tenantId, AddPlatformEstablishmentRequest request,
        HttpContext context, ICommandDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.DispatchAsync<AddPlatformEstablishmentCommand, PlatformProvisioningResult>(new(
            AccessToken(context), tenantId, request.IntentKey, request.Name, request.Slug, request.TimeZoneId,
            request.OwnerId), ct);
        return Results.Ok(Map(result));
    }

    private static async Task<IResult> SearchAsync(string? search, bool? isActive, int? page, int? pageSize,
        HttpContext context, IQueryDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.DispatchAsync<SearchPlatformTenantsQuery, PlatformTenantSearchResult>(new(
            AccessToken(context), search, isActive, page ?? 1, pageSize ?? 20), ct);
        return Results.Ok(new PlatformTenantPageResponse(result.Items.Select(Map).ToArray(), result.TotalCount,
            result.Page, result.PageSize));
    }

    private static async Task<IResult> GetAsync(Guid tenantId, HttpContext context,
        IQueryDispatcher dispatcher, CancellationToken ct) =>
        Results.Ok(Map(await dispatcher.DispatchAsync<GetPlatformTenantQuery, PlatformTenantReadModel>(
            new(AccessToken(context), tenantId), ct)));

    private static async Task<IResult> GetIntentAsync(Guid intentKey, HttpContext context,
        IQueryDispatcher dispatcher, CancellationToken ct) =>
        Results.Ok(Map(await dispatcher.DispatchAsync<GetPlatformProvisioningQuery, PlatformProvisioningResult>(
            new(AccessToken(context), intentKey), ct)));

    private static PlatformProvisioningResponse Map(PlatformProvisioningResult result) =>
        new(result.TenantId, result.EstablishmentId, result.OwnerId, result.TenantPublicCode,
            $"/administration/onboarding/dados");
    private static PlatformTenantResponse Map(PlatformTenantReadModel tenant) =>
        new(tenant.Id, tenant.Name, tenant.PublicCode, tenant.IsActive,
            tenant.Establishments.Select(x => new PlatformEstablishmentResponse(x.Id, x.Name, x.Slug,
                x.IsActive, x.OnboardingCompleted)).ToArray());
}
