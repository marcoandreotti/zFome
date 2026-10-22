using ZFome.Domain.Exceptions;

namespace ZFome.Domain.Tenancy;

/// <summary>
/// Representa o tipo de provisionamento da plataforma, podendo ser para um Tenant ou para um Estabelecimento.
/// </summary>
public enum PlatformProvisioningKind : short
{
    Tenant = 1,
    Establishment = 2
}

// Classe que representa a intenção de provisionamento da plataforma, podendo ser para um Tenant ou para um Estabelecimento.
public sealed class PlatformProvisioningIntent
{
    private PlatformProvisioningIntent()
    { }

    private PlatformProvisioningIntent(Guid id, Guid actorId, Guid key, string requestHash,
        PlatformProvisioningKind kind, DateTimeOffset now)
    {
        if (actorId == Guid.Empty || key == Guid.Empty || string.IsNullOrWhiteSpace(requestHash))
            throw new DomainException("Provisioning actor, key and request hash are required.");
        Id = id;
        ActorId = actorId;
        Key = key;
        RequestHash = requestHash;
        Kind = kind;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid Key { get; private set; }
    public string RequestHash { get; private set; } = string.Empty;
    public PlatformProvisioningKind Kind { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? EstablishmentId { get; private set; }
    public Guid? OwnerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static PlatformProvisioningIntent Create(Guid actorId, Guid key, string requestHash,
        PlatformProvisioningKind kind, DateTimeOffset now) =>
        new(Guid.NewGuid(), actorId, key, requestHash, kind, now);

    public void Complete(Guid tenantId, Guid establishmentId, Guid ownerId, DateTimeOffset now)
    {
        if (CompletedAt is not null) throw new DomainException("Provisioning intent is already complete.");
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty || ownerId == Guid.Empty)
            throw new DomainException("Provisioning result is incomplete.");
        TenantId = tenantId;
        EstablishmentId = establishmentId;
        OwnerId = ownerId;
        CompletedAt = now;
    }
}