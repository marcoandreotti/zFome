using ZFome.Domain.Exceptions;

namespace ZFome.Domain.Tenancy;

/// <summary>
/// Representa um tenant (inquilino) em um sistema multi-tenant, que é uma entidade que possui um conjunto de recursos e configurações específicas para um grupo de usuários ou clientes.
/// </summary>
public sealed class Tenant
{
    private Tenant()
    {
    }

    private Tenant(Guid id, string name, string publicCode, DateTimeOffset createdAt)
    {
        Id = id;
        SetName(name);
        PublicCode = NormalizePublicCode(publicCode);
        IsActive = true;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string PublicCode { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Cria um tenant ativo e aplica as invariantes do nome informado.</summary>
    public static Tenant Create(string name, DateTimeOffset now) =>
        new(Guid.NewGuid(), name, $"TEN-{Guid.NewGuid():N}"[..12], now);

    public static Tenant Create(string name, string publicCode, DateTimeOffset now) =>
        new(Guid.NewGuid(), name, publicCode, now);

    /// <summary>Altera o nome do tenant e atualiza a data da última modificação.</summary>
    public void Rename(string name, DateTimeOffset now)
    {
        SetName(name);
        UpdatedAt = now;
    }

    /// <summary>Desativa o tenant e, consequentemente, sua disponibilidade operacional.</summary>
    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>Reativa o tenant e registra a data da alteração.</summary>
    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAt = now;
    }

    private void SetName(string name)
    {
        var normalized = name.Trim();
        if (normalized.Length is < 1 or > 150)
        {
            throw new DomainException("Tenant name must contain 1 to 150 characters.");
        }

        Name = normalized;
    }

    public static string NormalizePublicCode(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length is < 3 or > 50 || normalized.Any(character => !char.IsLetterOrDigit(character) && character != '-'))
        {
            throw new DomainException("Tenant public code must contain 3 to 50 letters, numbers or hyphens.");
        }

        return normalized;
    }
}