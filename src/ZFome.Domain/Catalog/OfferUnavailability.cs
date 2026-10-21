using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Catalog;

public enum OfferKind
{
    Product,
    Variation,
    Additional
}

public sealed class OfferUnavailability : IEstablishmentScopedEntity
{
    private OfferUnavailability() { }

    private OfferUnavailability(Guid tenantId, Guid establishmentId, OfferKind kind, Guid offerId, DateTimeOffset startsAt, DateTimeOffset? endsAt, string? reason)
    {
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty || offerId == Guid.Empty || !Enum.IsDefined(kind) || endsAt <= startsAt)
            throw new DomainException("Offer unavailability is invalid.");
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EstablishmentId = establishmentId;
        Kind = kind;
        OfferId = offerId;
        StartsAt = startsAt;
        EndsAt = endsAt;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (Reason?.Length > 250) throw new DomainException("Availability reason is too long.");
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public OfferKind Kind { get; private set; }
    public Guid OfferId { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset? EndsAt { get; private set; }
    public DateTimeOffset? ReactivatedAt { get; private set; }
    public string? Reason { get; private set; }

    public static OfferUnavailability Create(Guid tenantId, Guid establishmentId, OfferKind kind, Guid offerId, DateTimeOffset startsAt, DateTimeOffset? endsAt, string? reason) =>
        new(tenantId, establishmentId, kind, offerId, startsAt, endsAt, reason);

    public bool IsActiveAt(DateTimeOffset instant) => ReactivatedAt is null && instant >= StartsAt && (EndsAt is null || instant < EndsAt);

    public void Reactivate(DateTimeOffset now)
    {
        if (now < StartsAt) throw new DomainException("An offer cannot be reactivated before unavailability starts.");
        ReactivatedAt ??= now;
    }
}
