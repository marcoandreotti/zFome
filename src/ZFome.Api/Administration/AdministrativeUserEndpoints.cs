using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Identity;
using ZFome.Application.Identity.CreateAdministrativeUser;
using ZFome.Application.Identity.Management;
using ZFome.Contracts.Administration;
using ZFome.Domain.Identity;

namespace ZFome.Api.Administration;

internal static class AdministrativeUserEndpoints
{
    public static void MapAdministrativeUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/establishments/{establishmentId:guid}/users")
            .RequireAuthorization(AdministrativePolicies.Administration).WithTags("Administrative users");
        group.AddEndpointFilter<UserManagementAuditFilter>();
        group.MapGet("", SearchAsync);
        group.MapPost("", CreateAsync);
        group.MapPut("/{userId:guid}", UpdateAsync);
        group.MapPatch("/{userId:guid}/active", SetActiveAsync);
        group.MapPut("/{userId:guid}/roles/{role}", SetRoleAsync);
        group.MapPut("/{userId:guid}/access", SetAccessAsync);
    }

    private static async Task<IResult> SearchAsync(Guid establishmentId, string? search, bool? isActive, int? page, int? pageSize, bool? associatedOnly, IQueryDispatcher dispatcher, CancellationToken ct)
    {
        var result = await dispatcher.DispatchAsync<SearchAdministrativeUsersQuery, AdministrativeUserSearchResult>(new(establishmentId, search, isActive, page ?? 1, pageSize ?? 20, associatedOnly ?? false), ct);
        return Results.Ok(new AdministrativeUserPageResponse(result.Items.Select(user => new AdministrativeUserResponse(user.Id, user.Name, user.Email, user.IsActive, user.Roles, user.EstablishmentIds, user.Id == result.ActorId)).ToArray(), result.TotalCount, result.Page, result.PageSize));
    }

    private static async Task<IResult> CreateAsync(Guid establishmentId, AdministrativeUserCreateRequest request, ICommandDispatcher dispatcher, CancellationToken ct)
    {
        var id = await dispatcher.DispatchAsync<CreateAdministrativeUserCommand, Guid>(new(request.Name, request.Email, request.Password, (AdministrativeRole)request.InitialRole, establishmentId), ct);
        return Results.Created($"/api/admin/establishments/{establishmentId}/users/{id}", new { id });
    }

    private static async Task<IResult> UpdateAsync(Guid establishmentId, Guid userId, AdministrativeUserUpdateRequest request, ICommandDispatcher dispatcher, CancellationToken ct)
    {
        await dispatcher.DispatchAsync(new UpdateAdministrativeUserCommand(establishmentId, userId, request.Name), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetActiveAsync(Guid establishmentId, Guid userId, AdministrativeUserActiveRequest request, ICommandDispatcher dispatcher, CancellationToken ct)
    {
        await dispatcher.DispatchAsync(new SetAdministrativeUserActiveCommand(establishmentId, userId, request.IsActive), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetRoleAsync(Guid establishmentId, Guid userId, short role, AdministrativeUserGrantRequest request, ICommandDispatcher dispatcher, CancellationToken ct)
    {
        await dispatcher.DispatchAsync(new SetAdministrativeUserRoleCommand(establishmentId, userId, (AdministrativeRole)role, request.Granted), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetAccessAsync(Guid establishmentId, Guid userId, AdministrativeUserGrantRequest request, ICommandDispatcher dispatcher, CancellationToken ct)
    {
        await dispatcher.DispatchAsync(new SetAdministrativeUserAccessCommand(establishmentId, userId, request.Granted), ct);
        return Results.NoContent();
    }
}

internal sealed class UserManagementAuditFilter(ILogger<UserManagementAuditFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        var result = await next(context);
        if (context.HttpContext.User.HasClaim("platform_user", "true"))
            logger.LogInformation("Platform actor {ActorId} completed {Method} {Path}; correlation {CorrelationId}",
                context.HttpContext.User.FindFirst("sub")?.Value, context.HttpContext.Request.Method,
                context.HttpContext.Request.Path, context.HttpContext.TraceIdentifier);
        return result;
    }
}
