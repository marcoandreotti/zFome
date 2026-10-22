using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Identity;

/// <summary>
/// Representa um usuário administrativo de um tenant, com informações sobre identidade, credenciais, papéis administrativos, acessos a estabelecimentos e status de atividade.
/// </summary>
public sealed class AdministrativeUser : ITenantScopedEntity
{
    private readonly List<AdministrativeUserRole> roleMemberships = [];
    private readonly List<UserEstablishmentAccess> establishmentAccesses = [];

    private AdministrativeUser()
    {
    }

    private AdministrativeUser(
        Guid id,
        Guid tenantId,
        string name,
        Email email,
        string passwordHash,
        AdministrativeRole initialRole,
        bool passwordChangeRequired,
        DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Tenant and password hash are required.");
        }

        Id = id;
        TenantId = tenantId;
        SetName(name);
        Email = email;
        NormalizedEmail = email.NormalizedValue;
        PasswordHash = passwordHash;
        PasswordChangeRequired = passwordChangeRequired;
        roleMemberships.Add(new AdministrativeUserRole(id, initialRole));
        IsActive = true;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool PasswordChangeRequired { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? LastAccessAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<AdministrativeUserRole> RoleMemberships => roleMemberships;
    public IReadOnlyCollection<UserEstablishmentAccess> EstablishmentAccesses => establishmentAccesses;

    /// <summary>Cria um usuário administrativo ativo com seu papel inicial.</summary>
    public static AdministrativeUser Create(
        Guid tenantId,
        string name,
        Email email,
        string passwordHash,
        AdministrativeRole initialRole,
        DateTimeOffset now) =>
        new(Guid.NewGuid(), tenantId, name, email, passwordHash, initialRole, false, now);

    /// <summary>Cria o primeiro Owner provisionado com troca obrigatória de senha.</summary>
    public static AdministrativeUser CreateProvisioned(
        Guid tenantId,
        string name,
        Email email,
        string passwordHash,
        DateTimeOffset now) =>
        new(Guid.NewGuid(), tenantId, name, email, passwordHash, AdministrativeRole.Owner, true, now);

    public void ChangePassword(string passwordHash, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        PasswordHash = passwordHash;
        PasswordChangeRequired = false;
        UpdatedAt = now;
    }

    /// <summary>Indica se o usuário está ativo e possui o papel administrativo informado.</summary>
    public bool HasRole(AdministrativeRole role) => IsActive && roleMemberships.Any(item => item.Role == role);

    /// <summary>Concede um papel administrativo sem duplicar associações existentes.</summary>
    public void GrantRole(AdministrativeRole role, DateTimeOffset now)
    {
        if (roleMemberships.All(item => item.Role != role))
        {
            roleMemberships.Add(new AdministrativeUserRole(Id, role));
        }
        UpdatedAt = now;
    }

    /// <summary>Concede ou reativa o acesso a um estabelecimento pertencente ao mesmo tenant.</summary>
    public void GrantEstablishmentAccess(Guid establishmentId, Guid establishmentTenantId, DateTimeOffset now)
    {
        if (establishmentTenantId != TenantId || establishmentId == Guid.Empty)
        {
            throw new DomainException("User and establishment must belong to the same tenant.");
        }

        var existing = establishmentAccesses.SingleOrDefault(item => item.EstablishmentId == establishmentId);
        if (existing is null)
        {
            establishmentAccesses.Add(new UserEstablishmentAccess(Id, TenantId, establishmentId, now));
        }
        else
        {
            existing.Activate();
        }

        UpdatedAt = now;
    }

    /// <summary>Revoga um acesso existente ao estabelecimento informado.</summary>
    public void RevokeEstablishmentAccess(Guid establishmentId, DateTimeOffset now)
    {
        var access = establishmentAccesses.SingleOrDefault(item => item.EstablishmentId == establishmentId)
            ?? throw new DomainException("Establishment access does not exist.");
        access.Revoke(now);
        UpdatedAt = now;
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>Atualiza os dados públicos sem alterar credenciais ou permissões.</summary>
    public void UpdateProfile(string name, DateTimeOffset now)
    {
        SetName(name);
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAt = now;
    }

    public void RevokeRole(AdministrativeRole role, DateTimeOffset now)
    {
        if (roleMemberships.Count == 1 && roleMemberships[0].Role == role)
            throw new DomainException("An administrative user must retain at least one role.");
        roleMemberships.RemoveAll(item => item.Role == role);
        UpdatedAt = now;
    }

    /// <summary>Registra um acesso bem-sucedido somente para usuários ativos.</summary>
    public void RecordSuccessfulAccess(DateTimeOffset now)
    {
        if (!IsActive)
        {
            throw new DomainException("Inactive users cannot record access.");
        }

        LastAccessAt = now;
        UpdatedAt = now;
    }

    private void SetName(string name)
    {
        var normalized = name.Trim();
        if (normalized.Length is < 1 or > 150)
        {
            throw new DomainException("User name must contain 1 to 150 characters.");
        }

        Name = normalized;
    }
}
