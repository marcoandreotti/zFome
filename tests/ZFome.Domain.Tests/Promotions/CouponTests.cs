using ZFome.Domain.Exceptions;
using ZFome.Domain.Promotions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Tests.Promotions;

public sealed class CouponTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Coupon_normalizes_code_and_calculates_percentage()
    {
        var coupon = Create(" promo-10 ", CouponDiscountType.Percentage, 10, new Money(20));
        var result = coupon.Evaluate(new Money(50), Now);
        Assert.Equal("PROMO10", coupon.Code); Assert.Equal(5m, result.Discount.Amount);
    }

    [Fact]
    public void Fixed_discount_is_limited_to_eligible_amount()
    {
        var coupon = Create("FIXO", CouponDiscountType.FixedAmount, 100, Money.Zero);
        Assert.Equal(30m, coupon.Evaluate(new Money(30), Now).Discount.Amount);
    }

    [Fact]
    public void Eligibility_enforces_window_activity_minimum_and_limit()
    {
        var coupon = Create("LIMIT", CouponDiscountType.FixedAmount, 5, new Money(20), 1);
        Assert.Throws<DomainException>(() => coupon.Evaluate(new Money(19), Now));
        coupon.Consume(Guid.NewGuid(), new Money(20), Now);
        Assert.Throws<DomainException>(() => coupon.Evaluate(new Money(20), Now));
        Assert.Throws<DomainException>(() => coupon.Evaluate(new Money(20), Now.AddDays(2)));
    }

    [Fact]
    public void Consumption_preserves_snapshot_and_rejects_duplicate_order()
    {
        var coupon = Create("SAVE", CouponDiscountType.FixedAmount, 5, Money.Zero, 2); var orderId = Guid.NewGuid();
        var use = coupon.Consume(orderId, new Money(20), Now); coupon.Update("NEWCODE", "changed", CouponDiscountType.FixedAmount, 2, Money.Zero, Now.AddDays(-1), Now.AddDays(2), 2, Now);
        Assert.Equal("SAVE", use.Code); Assert.Equal(5m, use.Discount.Amount);
        Assert.Throws<DomainException>(() => coupon.Consume(orderId, new Money(20), Now));
    }

    private static Coupon Create(string code, CouponDiscountType type, decimal value, Money minimum, int? limit = null) => Coupon.Create(Guid.NewGuid(), Guid.NewGuid(), code, null, type, value, minimum, Now.AddDays(-1), Now.AddDays(1), limit, Now);
}
