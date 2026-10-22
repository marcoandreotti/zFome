namespace ZFome.Domain.Identity;

/// <summary>
/// Representa os papéis administrativos disponíveis para usuários administrativos, com valores numéricos curtos para armazenamento eficiente.
/// </summary>
public enum AdministrativeRole : short
{
    Owner = 1,
    Admin = 2,
    Manager = 3,
    Attendant = 4,
    Kitchen = 5,
    Delivery = 6
}