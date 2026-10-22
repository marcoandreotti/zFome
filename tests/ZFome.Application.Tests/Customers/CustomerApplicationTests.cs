using FluentValidation;
using ZFome.Application.Abstractions.Customers;
using ZFome.Application.Abstractions.Tenancy;
using ZFome.Application.Customers;
using ZFome.Application.Exceptions;
using ZFome.Application.Tenancy;
using ZFome.Domain.Customers;

namespace ZFome.Application.Tests.Customers;

public sealed class CustomerApplicationTests
{
    [Fact]
    public async Task Upsert_creates_customer_in_authenticated_scope()
    {
        var establishmentId = Guid.NewGuid();
        var repository = new CustomerRepository();
        var handler = new UpsertCustomerCommandHandler(
            Resolver(establishmentId),
            repository,
            new Clock());

        var id = await handler.HandleAsync(new(establishmentId, null, "Maria", "(11) 99999-8888", null), CancellationToken.None);

        Assert.Equal(id, repository.Value!.Id);
        Assert.Equal(TenantContext.TenantIdValue, repository.Value.TenantId);
        Assert.Equal(establishmentId, repository.Value.EstablishmentId);
    }

    [Fact]
    public async Task Upsert_reuses_customer_with_same_normalized_phone_in_unit()
    {
        var establishmentId = Guid.NewGuid();
        var existing = Customer.Create(TenantContext.TenantIdValue, establishmentId, "Maria", "11999998888", null, Clock.Now);
        var repository = new CustomerRepository(existing);
        var handler = new UpsertCustomerCommandHandler(Resolver(establishmentId), repository, new Clock());

        var id = await handler.HandleAsync(new(establishmentId, null, "Maria Atualizada", "(11) 99999-8888", "maria@example.com"), CancellationToken.None);

        Assert.Equal(existing.Id, id);
        Assert.Equal("Maria Atualizada", existing.Name);
        Assert.False(repository.Added);
    }

    [Fact]
    public async Task Address_handler_switches_primary_inside_customer()
    {
        var establishmentId = Guid.NewGuid();
        var customer = Customer.Create(TenantContext.TenantIdValue, establishmentId, "Maria", "11999998888", null, Clock.Now);
        var first = customer.AddAddress("Casa", "Rua A", "1", null, "Centro", "São Paulo", "SP", "01001000", true, Clock.Now);
        var repository = new CustomerRepository(customer);
        var handler = new UpsertCustomerAddressCommandHandler(Resolver(establishmentId), repository, new Clock());

        var secondId = await handler.HandleAsync(new(establishmentId, customer.Id, null, "Trabalho", "Rua B", "2", null, "Centro", "São Paulo", "SP", "01002000", true), CancellationToken.None);

        Assert.False(first.IsPrimary);
        Assert.True(Assert.Single(customer.Addresses, address => address.Id == secondId).IsPrimary);
        Assert.True(repository.Saved);
    }

    [Fact]
    public async Task Search_passes_only_authenticated_scope_to_gateway()
    {
        var establishmentId = Guid.NewGuid();
        var gateway = new ReadGateway();
        var handler = new SearchCustomersQueryHandler(Resolver(establishmentId), gateway);

        await handler.HandleAsync(new(establishmentId, "maria", 2, 10), CancellationToken.None);

        Assert.Equal(TenantContext.TenantIdValue, gateway.TenantId);
        Assert.Equal(establishmentId, gateway.EstablishmentId);
        Assert.Equal(("maria", 2, 10), (gateway.Search, gateway.Page, gateway.PageSize));
    }

    [Fact]
    public async Task Search_rejects_unauthorized_establishment_before_gateway()
    {
        var gateway = new ReadGateway();
        var handler = new SearchCustomersQueryHandler(
            new EstablishmentScopeResolver(new TenantContext(), new AccessGateway(false)),
            gateway);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.HandleAsync(new(Guid.NewGuid(), null), CancellationToken.None));
        Assert.Equal(Guid.Empty, gateway.EstablishmentId);
    }

    [Fact]
    public async Task Validators_reject_malformed_inputs()
    {
        Assert.False((await new UpsertCustomerCommandValidator().ValidateAsync(new UpsertCustomerCommand(Guid.Empty, null, "", "123", "invalid"))).IsValid);
        Assert.False((await new UpsertCustomerAddressCommandValidator().ValidateAsync(new UpsertCustomerAddressCommand(Guid.Empty, Guid.Empty, null, "", "", "", null, "", "", "S", "", true))).IsValid);
        Assert.False((await new SearchCustomersQueryValidator().ValidateAsync(new SearchCustomersQuery(Guid.Empty, null, 0, 101))).IsValid);
    }

    private static EstablishmentScopeResolver Resolver(Guid establishmentId) =>
        new(new TenantContext(), new AccessGateway(true, establishmentId));

    private sealed class TenantContext : ITenantContext
    {
        public static readonly Guid TenantIdValue = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public bool HasTenant => true;
        public Guid TenantId => TenantIdValue;
        public bool HasUser => true;
        public Guid UserId => Guid.Parse("22222222-2222-2222-2222-222222222222");
        public Guid GetRequiredTenantId() => TenantId;
        public Guid GetRequiredUserId() => UserId;
    }

    private sealed class AccessGateway(bool allowed, Guid? expectedEstablishmentId = null) : IEstablishmentAccessGateway
    {
        public Task<bool> HasActiveAccessAsync(Guid tenantId, Guid userId, Guid establishmentId, CancellationToken cancellationToken) =>
            Task.FromResult(allowed && (!expectedEstablishmentId.HasValue || expectedEstablishmentId == establishmentId));
    }

    private sealed class CustomerRepository(Customer? value = null) : ICustomerRepository
    {
        public Customer? Value { get; private set; } = value;
        public bool Added { get; private set; }
        public bool Saved { get; private set; }
        public Task<Customer?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Value is { } customer && customer.TenantId == tenantId && customer.EstablishmentId == establishmentId && customer.Id == id ? customer : null);
        public Task<Customer?> FindByNormalizedPhoneAsync(Guid tenantId, Guid establishmentId, string normalizedPhone, CancellationToken cancellationToken) =>
            Task.FromResult(Value is { } customer && customer.TenantId == tenantId && customer.EstablishmentId == establishmentId && customer.NormalizedPhone == normalizedPhone ? customer : null);
        public Task AddAsync(Customer customer, CancellationToken cancellationToken)
        {
            Value = customer;
            Added = true;
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class ReadGateway : ICustomerReadGateway
    {
        public Guid TenantId { get; private set; }
        public Guid EstablishmentId { get; private set; }
        public string? Search { get; private set; }
        public int Page { get; private set; }
        public int PageSize { get; private set; }

        public Task<CustomerSearchResult> SearchAsync(Guid tenantId, Guid establishmentId, string? search, int page, int pageSize, CancellationToken cancellationToken)
        {
            TenantId = tenantId;
            EstablishmentId = establishmentId;
            Search = search;
            Page = page;
            PageSize = pageSize;
            return Task.FromResult(new CustomerSearchResult(0, []));
        }
    }

    private sealed class Clock : TimeProvider
    {
        public static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
