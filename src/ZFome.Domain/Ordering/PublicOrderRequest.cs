using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Ordering;

/// <summary>
/// Representa um pedido público que é usado para garantir a idempotência de solicitações de pedidos.
/// Registra o resultado imutável de uma confirmação pública idempotente.
/// </summary>
public sealed class PublicOrderRequest : IEstablishmentScopedEntity
{
    private PublicOrderRequest()
    { }

    private PublicOrderRequest(Guid tenantId, Guid establishmentId, string key, string payloadHash, Guid orderId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; Key = key; PayloadHash = payloadHash; OrderId = orderId; CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public Guid OrderId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static PublicOrderRequest Create(Guid tenantId, Guid establishmentId, string key, string payloadHash, Guid orderId, DateTimeOffset now)
    {
        var normalized = key.Trim();
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty || orderId == Guid.Empty || normalized.Length is < 8 or > 100 || payloadHash.Length != 64)
            throw new DomainException("Public order idempotency data is invalid.");
        return new(tenantId, establishmentId, normalized, payloadHash, orderId, now);
    }
}