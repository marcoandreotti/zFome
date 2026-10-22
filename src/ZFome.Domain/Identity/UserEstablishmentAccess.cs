namespace ZFome.Domain.Identity;

/// <summary>
/// Representa o acesso de um usuário a um estabelecimento, com informações sobre o usuário, estabelecimento, status de atividade e datas de criação e revogação.
/// </summary>
public sealed class UserEstablishmentAccess
{
    private UserEstablishmentAccess()
    { }

    internal UserEstablishmentAccess(Guid userId, Guid tenantId, Guid establishmentId, DateTimeOffset now)
    { UserId = userId; TenantId = tenantId; EstablishmentId = establishmentId; IsActive = true; CreatedAt = now; }

    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    internal void Activate()
    { IsActive = true; RevokedAt = null; }

    internal void Revoke(DateTimeOffset now)
    { IsActive = false; RevokedAt = now; }
}