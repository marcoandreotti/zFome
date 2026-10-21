using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Delivery;

/// <summary>A tenant-owned postal-code range used to price and estimate a delivery.</summary>
public sealed class DeliveryRegion : IEstablishmentScopedEntity
{
    private DeliveryRegion() { }

    private DeliveryRegion(Guid tenantId, Guid establishmentId, string name, string postalCodeFrom,
        string postalCodeTo, Money fee, int estimatedMinutes)
    {
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty)
            throw new DomainException("Delivery region scope is required.");
        Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId;
        Update(name, postalCodeFrom, postalCodeTo, fee, estimatedMinutes, false);
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string PostalCodeFrom { get; private set; } = string.Empty;
    public string PostalCodeTo { get; private set; } = string.Empty;
    public Money Fee { get; private set; }
    public int EstimatedMinutes { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static DeliveryRegion Create(Guid tenantId, Guid establishmentId, string name,
        string postalCodeFrom, string postalCodeTo, Money fee, int estimatedMinutes, DateTimeOffset now)
    {
        var result = new DeliveryRegion(tenantId, establishmentId, name, postalCodeFrom, postalCodeTo, fee, estimatedMinutes);
        result.UpdatedAt = now;
        return result;
    }

    public void Update(string name, string postalCodeFrom, string postalCodeTo, Money fee,
        int estimatedMinutes, DateTimeOffset now)
    {
        Update(name, postalCodeFrom, postalCodeTo, fee, estimatedMinutes, true);
        UpdatedAt = now;
    }

    public void SetActive(bool isActive, DateTimeOffset now)
    { IsActive = isActive; UpdatedAt = now; }

    public bool Covers(string normalizedPostalCode) => IsActive &&
        string.CompareOrdinal(normalizedPostalCode, PostalCodeFrom) >= 0 &&
        string.CompareOrdinal(normalizedPostalCode, PostalCodeTo) <= 0;

    public static string NormalizePostalCode(string value)
    {
        var normalized = new string(value.Where(char.IsDigit).ToArray());
        if (normalized.Length != 8) throw new DomainException("Postal code is invalid.");
        return normalized;
    }

    private void Update(string name, string postalCodeFrom, string postalCodeTo, Money fee,
        int estimatedMinutes, bool preserveActive)
    {
        var normalizedName = name.Trim();
        if (normalizedName.Length is < 1 or > 100) throw new DomainException("Delivery region name is invalid.");
        var from = NormalizePostalCode(postalCodeFrom);
        var to = NormalizePostalCode(postalCodeTo);
        if (string.CompareOrdinal(from, to) > 0) throw new DomainException("Postal code range is invalid.");
        if (fee.Amount < 0) throw new DomainException("Delivery fee cannot be negative.");
        if (estimatedMinutes is < 1 or > 1440) throw new DomainException("Delivery estimate is invalid.");
        Name = normalizedName; PostalCodeFrom = from; PostalCodeTo = to; Fee = fee; EstimatedMinutes = estimatedMinutes;
        if (!preserveActive) IsActive = false;
    }
}

public sealed record DeliveryQuote(Guid RegionId, string RegionName, Money Fee, int EstimatedMinutes);
