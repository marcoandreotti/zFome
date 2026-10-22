using ZFome.Application.Catalog;

namespace ZFome.Application.Tests.Catalog;

public sealed class CatalogMaintenanceValidationTests
{
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(1000001, 20)]
    public void Both_queries_reject_invalid_pagination(int page, int pageSize)
    {
        Assert.False(new SearchAdditionalsQueryValidator().Validate(new SearchAdditionalsQuery(Guid.NewGuid(), Page: page, PageSize: pageSize)).IsValid);
        Assert.False(new SearchAdditionalGroupsQueryValidator().Validate(new SearchAdditionalGroupsQuery(Guid.NewGuid(), Page: page, PageSize: pageSize)).IsValid);
    }

    [Fact]
    public void Validates_scope_search_and_defaults()
    {
        Assert.False(new SearchAdditionalsQueryValidator().Validate(new SearchAdditionalsQuery(Guid.Empty)).IsValid);
        Assert.False(new SearchAdditionalGroupsQueryValidator().Validate(new SearchAdditionalGroupsQuery(Guid.NewGuid(), new string('x', 151))).IsValid);
        Assert.True(new SearchAdditionalsQueryValidator().Validate(new SearchAdditionalsQuery(Guid.NewGuid())).IsValid);
        Assert.True(new SearchAdditionalGroupsQueryValidator().Validate(new SearchAdditionalGroupsQuery(Guid.NewGuid())).IsValid);
    }
}
