using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Tests.SharedKernel;

public sealed class MoneyTests
{
    [Fact]
    public void Constructor_rounds_to_two_decimal_places_away_from_zero()
    {
        Assert.Equal(10.01m, new Money(10.005m).Amount);
    }

    [Fact]
    public void Constructor_rejects_negative_values()
    {
        Assert.Throws<DomainException>(() => new Money(-0.01m));
    }

    [Fact]
    public void Arithmetic_preserves_money_invariants()
    {
        var total = new Money(10m) * new Quantity(2.5m);

        Assert.Equal(new Money(25m), total);
        Assert.Equal(new Money(20m), total - new Money(5m));
        Assert.Throws<DomainException>(() => new Money(5m) - new Money(6m));
    }
}
