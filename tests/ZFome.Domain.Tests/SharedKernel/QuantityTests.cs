using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Tests.SharedKernel;

public sealed class QuantityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.0004)]
    public void Constructor_rejects_values_outside_supported_range(decimal value)
    {
        Assert.Throws<DomainException>(() => new Quantity(value));
    }

    [Fact]
    public void Constructor_rounds_to_three_decimal_places()
    {
        Assert.Equal(1.235m, new Quantity(1.2345m).Value);
    }
}
