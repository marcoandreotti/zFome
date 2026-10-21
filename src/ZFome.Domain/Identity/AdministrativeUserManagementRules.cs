namespace ZFome.Domain.Identity;

/// <summary>Regras de hierarquia independentes de HTTP, claims e persistência.</summary>
public static class AdministrativeUserManagementRules
{
    public static bool CanManage(AdministrativeUser actor) =>
        actor.HasRole(AdministrativeRole.Owner) || actor.HasRole(AdministrativeRole.Admin);

    public static bool CanManageOwner(AdministrativeUser actor, Guid targetId) =>
        actor.HasRole(AdministrativeRole.Owner) && actor.Id != targetId;

    // HasRole considera atividade; a proteção do destinatário também abrange Owners inativos.
    public static bool IsOwner(AdministrativeUser user) =>
        user.RoleMemberships.Any(role => role.Role == AdministrativeRole.Owner);

    public static bool PreservesActiveOwner(int otherActiveOwners, bool targetActive, IEnumerable<AdministrativeRole> targetRoles) =>
        otherActiveOwners > 0 || (targetActive && targetRoles.Contains(AdministrativeRole.Owner));

    public static bool PreservesAdministrator(int otherActiveAdministrators, bool targetActive, IEnumerable<AdministrativeRole> targetRoles) =>
        otherActiveAdministrators > 0 || (targetActive && targetRoles.Any(role => role is AdministrativeRole.Owner or AdministrativeRole.Admin));
}
