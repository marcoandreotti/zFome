using ZFome.Application.Reporting;

namespace ZFome.Application.Tests.Reporting;

public sealed class BusinessDashboardValidationTests
{
    [Fact]
    public async Task Accepts_a_period_of_up_to_366_inclusive_days()
    {
        var validator = new GetBusinessDashboardQueryValidator();
        var query = new GetBusinessDashboardQuery(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));

        Assert.True((await validator.ValidateAsync(query)).IsValid);
    }

    [Fact]
    public async Task Rejects_reversed_or_long_periods_and_invalid_product_paging()
    {
        var validator = new GetBusinessDashboardQueryValidator();
        var reversed = new GetBusinessDashboardQuery(
            Guid.NewGuid(), new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 31));
        var tooLong = new GetBusinessDashboardQuery(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2));
        var unboundedPage = new GetBusinessDashboardQuery(
            Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), ProductPageSize: 101);

        Assert.False((await validator.ValidateAsync(reversed)).IsValid);
        Assert.False((await validator.ValidateAsync(tooLong)).IsValid);
        Assert.False((await validator.ValidateAsync(unboundedPage)).IsValid);
    }
}
