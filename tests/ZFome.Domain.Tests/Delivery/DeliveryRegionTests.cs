using ZFome.Domain.Delivery;
using ZFome.Domain.Exceptions;
using ZFome.Domain.SharedKernel;

namespace ZFome.Domain.Tests.Delivery;

public sealed class DeliveryRegionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Postal_code_is_normalized_and_range_coverage_is_deterministic()
    {
        var region = DeliveryRegion.Create(Guid.NewGuid(), Guid.NewGuid(), "Centro", "01000-000", "01099-999", new Money(5), 40, Now);
        region.SetActive(true, Now);

        Assert.Equal("01000000", DeliveryRegion.NormalizePostalCode("01000-000"));
        Assert.True(region.Covers("01050000"));
        Assert.False(region.Covers("01100000"));
    }

    [Fact]
    public void Invalid_postal_ranges_and_estimates_are_rejected()
    {
        Assert.Throws<DomainException>(() => DeliveryRegion.Create(Guid.NewGuid(), Guid.NewGuid(), "Centro", "01099-999", "01000-000", new Money(5), 40, Now));
        Assert.Throws<DomainException>(() => DeliveryRegion.Create(Guid.NewGuid(), Guid.NewGuid(), "Centro", "01000-000", "01099-999", new Money(5), 0, Now));
        Assert.Throws<DomainException>(() => DeliveryRegion.NormalizePostalCode("123"));
    }
}
