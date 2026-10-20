using OrderHub.Domain.Customers;
using OrderHub.Domain.Exceptions;

namespace OrderHub.Domain.Tests.Customers;

public sealed class CustomerTests
{
    [Fact]
    public void Customer_normalizes_contact_and_accepts_missing_email()
    {
        var customer = Customer.Create(Guid.NewGuid(), Guid.NewGuid(), "  Maria Silva  ", "(11) 99999-8888", null, DateTimeOffset.UtcNow);

        Assert.Equal("Maria Silva", customer.Name);
        Assert.Equal("11999998888", customer.NormalizedPhone);
        Assert.Null(customer.Email);
    }

    [Theory]
    [InlineData("+55 (0)19 99999-9999", "19999999999")]
    [InlineData("+55019999999999", "19999999999")]
    [InlineData("55019999999999", "19999999999")]
    [InlineData("019999999999", "19999999999")]
    [InlineData("19999999999", "19999999999")]
    [InlineData("+5555999999999", "55999999999")]
    [InlineData("55199999999999", "55199999999999")]
    [InlineData("55020999999999", "55020999999999")]
    [InlineData("+1 (212) 555-0100", "12125550100")]
    public void Phone_normalization_is_idempotent_and_preserves_other_valid_formats(string input, string expected)
    {
        var normalized = Customer.NormalizePhone(input);

        Assert.Equal(expected, normalized);
        Assert.Equal(normalized, Customer.NormalizePhone(normalized));
    }

    [Fact]
    public void Ddd_55_without_country_code_is_preserved()
    {
        Assert.Equal("55999999999", Customer.NormalizePhone("55999999999"));
    }

    [Fact]
    public void Customer_rejects_invalid_scope_and_contact()
    {
        Assert.Throws<DomainException>(() => Customer.Create(Guid.Empty, Guid.NewGuid(), "Maria", "11999998888", null, DateTimeOffset.UtcNow));
        Assert.Throws<DomainException>(() => Customer.Create(Guid.NewGuid(), Guid.NewGuid(), "Maria", "123", null, DateTimeOffset.UtcNow));
        Assert.Throws<DomainException>(() => Customer.Create(Guid.NewGuid(), Guid.NewGuid(), "Maria", "11999998888", "invalid", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Customer_keeps_only_one_primary_address()
    {
        var now = DateTimeOffset.UtcNow;
        var customer = Customer.Create(Guid.NewGuid(), Guid.NewGuid(), "Maria", "11999998888", null, now);
        var first = customer.AddAddress("Casa", "Rua A", "10", null, "Centro", "São Paulo", "SP", "01001000", true, now);
        var second = customer.AddAddress("Trabalho", "Rua B", "20", null, "Centro", "São Paulo", "SP", "01002000", true, now);

        Assert.False(first.IsPrimary);
        Assert.True(second.IsPrimary);
        Assert.Single(customer.Addresses, address => address.IsPrimary);
    }

    [Fact]
    public void Customer_cannot_change_address_from_another_aggregate()
    {
        var customer = Customer.Create(Guid.NewGuid(), Guid.NewGuid(), "Maria", "11999998888", null, DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => customer.UpdateAddress(Guid.NewGuid(), "Casa", "Rua A", "1", null, "Centro", "São Paulo", "SP", "01001000", true, DateTimeOffset.UtcNow));
    }
}
