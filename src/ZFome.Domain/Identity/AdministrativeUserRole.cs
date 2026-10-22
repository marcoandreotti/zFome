namespace ZFome.Domain.Identity;

/// <summary>
/// Representa a associação de um usuário administrativo a um papel administrativo, com informações sobre o usuário e o papel.
/// </summary>
public sealed class AdministrativeUserRole
{
    private AdministrativeUserRole()
    { }

    internal AdministrativeUserRole(Guid userId, AdministrativeRole role)
    { UserId = userId; Role = role; }

    public Guid UserId { get; private set; }
    public AdministrativeRole Role { get; private set; }
}