using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Promotions;

public enum CouponDiscountType
{ Percentage, FixedAmount }

public sealed record CouponEvaluation(Guid CouponId, string Code, Money Discount);

/// <summary>
/// Representa um cupom de desconto no escopo de um estabelecimento, com regras de validade, elegibilidade e uso.
/// </summary>
public sealed class Coupon : IEstablishmentScopedEntity
{
    private readonly List<CouponUse> uses = [];

    private Coupon()
    { }

    private Coupon(Guid tenantId, Guid establishmentId, string code, string? description, CouponDiscountType discountType, decimal value, Money minimumOrder, DateTimeOffset startsAt, DateTimeOffset endsAt, int? maximumUses, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || establishmentId == Guid.Empty) throw new DomainException("Coupon scope is required.");
        Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; CreatedAt = now;
        Update(code, description, discountType, value, minimumOrder, startsAt, endsAt, maximumUses, now);
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public CouponDiscountType DiscountType { get; private set; }
    public decimal Value { get; private set; }
    public Money MinimumOrder { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public int? MaximumUses { get; private set; }
    public int UsedCount { get; private set; }
    public bool IsActive { get; private set; }
    public Guid Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<CouponUse> Uses => uses;

    /// <summary>Cria um cupom ativo e normalizado no escopo do estabelecimento.</summary>
    public static Coupon Create(Guid tenantId, Guid establishmentId, string code, string? description, CouponDiscountType discountType, decimal value, Money minimumOrder, DateTimeOffset startsAt, DateTimeOffset endsAt, int? maximumUses, DateTimeOffset now) =>
        new(tenantId, establishmentId, code, description, discountType, value, minimumOrder, startsAt, endsAt, maximumUses, now);

    /// <summary>Atualiza as regras do cupom sem alterar usos já registrados.</summary>
    public void Update(string code, string? description, CouponDiscountType discountType, decimal value, Money minimumOrder, DateTimeOffset startsAt, DateTimeOffset endsAt, int? maximumUses, DateTimeOffset now)
    {
        if (startsAt >= endsAt) throw new DomainException("Coupon validity window is invalid.");
        if (maximumUses is <= 0 || maximumUses < UsedCount) throw new DomainException("Coupon usage limit is invalid.");
        var normalizedValue = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        if (normalizedValue <= 0 || discountType == CouponDiscountType.Percentage && normalizedValue > 100) throw new DomainException("Coupon discount value is invalid.");
        Code = NormalizeCode(code); Description = NormalizeDescription(description); DiscountType = discountType; Value = normalizedValue;
        MinimumOrder = minimumOrder; StartsAt = startsAt; EndsAt = endsAt; MaximumUses = maximumUses; Touch(now);
    }

    public void Activate(DateTimeOffset now)
    { IsActive = true; Touch(now); }

    public void Deactivate(DateTimeOffset now)
    { IsActive = false; Touch(now); }

    /// <summary>Avalia o desconto usando o subtotal autoritativo do pedido.</summary>
    public CouponEvaluation Evaluate(Money eligibleAmount, DateTimeOffset now)
    {
        EnsureEligible(eligibleAmount, now);
        var calculated = DiscountType == CouponDiscountType.Percentage ? eligibleAmount.Amount * Value / 100m : Value;
        return new CouponEvaluation(Id, Code, new Money(Math.Min(eligibleAmount.Amount, calculated)));
    }

    /// <summary>Registra uma utilização única após revalidar todas as regras de elegibilidade.</summary>
    public CouponUse Consume(Guid orderId, Money eligibleAmount, DateTimeOffset now)
    {
        if (orderId == Guid.Empty) throw new DomainException("Coupon use requires an order.");
        var evaluation = Evaluate(eligibleAmount, now);
        if (uses.Any(x => x.OrderId == orderId)) throw new DomainException("Coupon was already consumed by this order.");
        var use = new CouponUse(TenantId, EstablishmentId, Id, orderId, evaluation.Code, evaluation.Discount, now);
        uses.Add(use); UsedCount++; Touch(now); return use;
    }

    public static string NormalizeCode(string code)
    {
        var normalized = new string(code.Trim().Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (normalized.Length is < 3 or > 40) throw new DomainException("Coupon code is invalid.");
        return normalized;
    }

    private void EnsureEligible(Money eligibleAmount, DateTimeOffset now)
    {
        if (!IsActive) throw new DomainException("Coupon is inactive.");
        if (now < StartsAt || now >= EndsAt) throw new DomainException("Coupon is outside its validity window.");
        if (eligibleAmount.Amount < MinimumOrder.Amount) throw new DomainException("Order does not reach the coupon minimum.");
        if (MaximumUses is { } limit && UsedCount >= limit) throw new DomainException("Coupon usage limit was reached.");
    }

    private void Touch(DateTimeOffset now)
    { UpdatedAt = now; Version = Guid.NewGuid(); }

    private static string? NormalizeDescription(string? value)
    { if (string.IsNullOrWhiteSpace(value)) return null; var normalized = value.Trim(); if (normalized.Length > 300) throw new DomainException("Coupon description is invalid."); return normalized; }
}

public sealed class CouponUse : IEstablishmentScopedEntity
{
    private CouponUse()
    { }

    internal CouponUse(Guid tenantId, Guid establishmentId, Guid couponId, Guid orderId, string code, Money discount, DateTimeOffset usedAt)
    { Id = Guid.NewGuid(); TenantId = tenantId; EstablishmentId = establishmentId; CouponId = couponId; OrderId = orderId; Code = code; Discount = discount; UsedAt = usedAt; }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public Guid CouponId { get; private set; }
    public Guid OrderId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public Money Discount { get; private set; }
    public DateTimeOffset UsedAt { get; private set; }
}