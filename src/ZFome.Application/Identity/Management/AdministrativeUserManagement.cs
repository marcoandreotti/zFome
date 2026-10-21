using FluentValidation;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Exceptions;
using ZFome.Application.Tenancy;
using ZFome.Domain.Identity;

namespace ZFome.Application.Identity.Management;

public sealed record AdministrativeUserReadModel
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public short[] Roles { get; init; } = [];
    public Guid[] EstablishmentIds { get; init; } = [];
}
public sealed record AdministrativeUserSearchResult(IReadOnlyList<AdministrativeUserReadModel> Items, long TotalCount, int Page, int PageSize, Guid ActorId = default);
public sealed record SearchAdministrativeUsersQuery(Guid EstablishmentId, string? Search = null, bool? IsActive = null, int Page = 1, int PageSize = 20, bool AssociatedOnly = false) : IQuery<AdministrativeUserSearchResult>;
public sealed record UpdateAdministrativeUserCommand(Guid EstablishmentId, Guid UserId, string Name) : ICommand;
public sealed record SetAdministrativeUserActiveCommand(Guid EstablishmentId, Guid UserId, bool IsActive) : ICommand;
public sealed record SetAdministrativeUserRoleCommand(Guid EstablishmentId, Guid UserId, AdministrativeRole Role, bool Granted) : ICommand;
public sealed record SetAdministrativeUserAccessCommand(Guid EstablishmentId, Guid UserId, bool Granted) : ICommand;

/// <summary>Coordena escrita atômica e revalida o ator depois do bloqueio do Tenant.</summary>
public sealed class AdministrativeUserManagement(
    EstablishmentScopeResolver scopeResolver,
    ITenantContext tenantContext,
    IAdministrativeUserManagementRepository repository,
    IAdministrativeUserManagementTransaction transaction)
{
    public async Task ExecuteAsync(Guid establishmentId, Func<OperationalScope, AdministrativeUser?, CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(establishmentId, cancellationToken);
        await transaction.ExecuteAsync(scope.TenantId, async ct =>
        {
            AdministrativeUser? actor = null;
            if (!await repository.IsActiveEstablishmentAsync(scope.TenantId, establishmentId, ct))
                throw new ForbiddenException("An authorized establishment context is required.");
            if (tenantContext.IsPlatformUser)
            {
                if (!await repository.IsEligiblePlatformUserAsync(scope.UserId, ct))
                    throw new ForbiddenException("An active platform identity is required.");
            }
            else
            {
                actor = await repository.GetAsync(scope.TenantId, scope.UserId, ct);
                if (actor is null || !AdministrativeUserManagementRules.CanManage(actor) ||
                    !actor.EstablishmentAccesses.Any(access => access.EstablishmentId == establishmentId && access.IsActive))
                    throw new ForbiddenException("User administration is not permitted.");
            }
            await operation(scope, actor, ct);
        }, cancellationToken);
    }

    public static void RequireOwner(AdministrativeUser? actor, Guid targetId)
    {
        // null representa exclusivamente o ator global já validado em ExecuteAsync.
        if (actor is not null && !AdministrativeUserManagementRules.CanManageOwner(actor, targetId))
            throw new ForbiddenException("Only another active Owner may manage this Owner permission or state.");
    }

    public async Task<AdministrativeUser> GetTargetAsync(Guid tenantId, Guid userId, CancellationToken ct) =>
        await repository.GetAsync(tenantId, userId, ct) ?? throw new NotFoundException("Administrative user was not found.");

    public async Task RequireContinuityAsync(AdministrativeUser user, bool active, IEnumerable<AdministrativeRole> roles, CancellationToken ct, Guid? revokedEstablishmentId = null)
    {
        var roleSet = roles.ToArray();
        foreach (var access in user.EstablishmentAccesses.Where(access => access.IsActive))
        {
            if (!await repository.IsActiveEstablishmentAsync(user.TenantId, access.EstablishmentId, ct)) continue;
            var remainsEligible = active && access.EstablishmentId != revokedEstablishmentId && roleSet.Any(role => role is AdministrativeRole.Owner or AdministrativeRole.Admin);
            var others = await repository.CountOtherUnitAdministratorsAsync(user.TenantId, access.EstablishmentId, user.Id, ct);
            if (!AdministrativeUserManagementRules.PreservesAdministrator(others, remainsEligible, roleSet))
                throw new ConflictException("The establishment must retain an active administrator.");
        }
        var counts = await repository.CountOtherAdministratorsAsync(user.TenantId, user.Id, ct);
        var hasAccess = false;
        foreach (var access in user.EstablishmentAccesses.Where(access => access.IsActive && access.EstablishmentId != revokedEstablishmentId))
            hasAccess |= await repository.IsActiveEstablishmentAsync(user.TenantId, access.EstablishmentId, ct);
        if (!AdministrativeUserManagementRules.PreservesAdministrator(counts.Administrators, active && hasAccess, roles))
            throw new ConflictException("The tenant must retain an active administrator.");
        if (!AdministrativeUserManagementRules.PreservesActiveOwner(counts.Owners, active, roles))
            throw new ConflictException("The tenant must retain an active Owner.");
    }
}

public sealed class UpdateAdministrativeUserCommandHandler(AdministrativeUserManagement management, IAdministrativeUserManagementRepository repository, TimeProvider time) : ICommandHandler<UpdateAdministrativeUserCommand>
{
    public Task HandleAsync(UpdateAdministrativeUserCommand command, CancellationToken ct) => management.ExecuteAsync(command.EstablishmentId, async (scope, actor, token) =>
    {
        var user = await management.GetTargetAsync(scope.TenantId, command.UserId, token);
        user.UpdateProfile(command.Name, time.GetUtcNow());
        await repository.SaveAsync(token);
    }, ct);
}

public sealed class SetAdministrativeUserActiveCommandHandler(AdministrativeUserManagement management, IAdministrativeUserManagementRepository repository, TimeProvider time) : ICommandHandler<SetAdministrativeUserActiveCommand>
{
    public Task HandleAsync(SetAdministrativeUserActiveCommand command, CancellationToken ct) => management.ExecuteAsync(command.EstablishmentId, async (scope, actor, token) =>
    {
        var user = await management.GetTargetAsync(scope.TenantId, command.UserId, token);
        if (AdministrativeUserManagementRules.IsOwner(user)) AdministrativeUserManagement.RequireOwner(actor, user.Id);
        await management.RequireContinuityAsync(user, command.IsActive, user.RoleMemberships.Select(role => role.Role), token);
        if (command.IsActive) user.Activate(time.GetUtcNow()); else user.Deactivate(time.GetUtcNow());
        await repository.SaveAsync(token);
    }, ct);
}

public sealed class SetAdministrativeUserRoleCommandHandler(AdministrativeUserManagement management, IAdministrativeUserManagementRepository repository, TimeProvider time) : ICommandHandler<SetAdministrativeUserRoleCommand>
{
    public Task HandleAsync(SetAdministrativeUserRoleCommand command, CancellationToken ct) => management.ExecuteAsync(command.EstablishmentId, async (scope, actor, token) =>
    {
        var user = await management.GetTargetAsync(scope.TenantId, command.UserId, token);
        if (command.Role == AdministrativeRole.Owner) AdministrativeUserManagement.RequireOwner(actor, user.Id);
        var roles = user.RoleMemberships.Select(role => role.Role).ToHashSet();
        if (command.Granted) roles.Add(command.Role); else roles.Remove(command.Role);
        await management.RequireContinuityAsync(user, user.IsActive, roles, token);
        if (command.Granted) user.GrantRole(command.Role, time.GetUtcNow()); else user.RevokeRole(command.Role, time.GetUtcNow());
        await repository.SaveAsync(token);
    }, ct);
}

public sealed class SetAdministrativeUserAccessCommandHandler(AdministrativeUserManagement management, IAdministrativeUserManagementRepository repository, TimeProvider time) : ICommandHandler<SetAdministrativeUserAccessCommand>
{
    public Task HandleAsync(SetAdministrativeUserAccessCommand command, CancellationToken ct) => management.ExecuteAsync(command.EstablishmentId, async (scope, actor, token) =>
    {
        var user = await management.GetTargetAsync(scope.TenantId, command.UserId, token);
        if (command.Granted && !user.IsActive) throw new ConflictException("Only active users may be associated.");
        if (command.Granted) user.GrantEstablishmentAccess(scope.EstablishmentId, scope.TenantId, time.GetUtcNow());
        else if (user.EstablishmentAccesses.Any(access => access.EstablishmentId == scope.EstablishmentId))
        {
            await management.RequireContinuityAsync(user, user.IsActive, user.RoleMemberships.Select(role => role.Role), token, scope.EstablishmentId);
            user.RevokeEstablishmentAccess(scope.EstablishmentId, time.GetUtcNow());
        }
        await repository.SaveAsync(token);
    }, ct);
}

public sealed class SearchAdministrativeUsersQueryHandler(EstablishmentScopeResolver scopeResolver, ITenantContext tenantContext, IAdministrativeUserReadGateway gateway) : IQueryHandler<SearchAdministrativeUsersQuery, AdministrativeUserSearchResult>
{
    public async Task<AdministrativeUserSearchResult> HandleAsync(SearchAdministrativeUsersQuery query, CancellationToken ct)
    {
        var scope = await scopeResolver.ResolveAsync(query.EstablishmentId, ct);
        if (!await gateway.CanManageAsync(scope.TenantId, scope.UserId, tenantContext.IsPlatformUser, ct))
            throw new ForbiddenException("User administration is not permitted.");
        var result = await gateway.SearchAsync(scope.TenantId, query, ct);
        return result with { ActorId = tenantContext.IsPlatformUser ? Guid.Empty : scope.UserId };
    }
}

public sealed class SearchAdministrativeUsersQueryValidator : AbstractValidator<SearchAdministrativeUsersQuery>
{
    public SearchAdministrativeUsersQueryValidator()
    {
        RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.Search).MaximumLength(150);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
public sealed class UpdateAdministrativeUserCommandValidator : AbstractValidator<UpdateAdministrativeUserCommand>
{
    public UpdateAdministrativeUserCommandValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.UserId).NotEmpty(); RuleFor(x => x.Name).NotEmpty().MaximumLength(150); }
}
public sealed class SetAdministrativeUserActiveCommandValidator : AbstractValidator<SetAdministrativeUserActiveCommand>
{
    public SetAdministrativeUserActiveCommandValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.UserId).NotEmpty(); }
}
public sealed class SetAdministrativeUserRoleCommandValidator : AbstractValidator<SetAdministrativeUserRoleCommand>
{
    public SetAdministrativeUserRoleCommandValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.UserId).NotEmpty(); RuleFor(x => x.Role).IsInEnum(); }
}
public sealed class SetAdministrativeUserAccessCommandValidator : AbstractValidator<SetAdministrativeUserAccessCommand>
{
    public SetAdministrativeUserAccessCommandValidator() { RuleFor(x => x.EstablishmentId).NotEmpty(); RuleFor(x => x.UserId).NotEmpty(); }
}
