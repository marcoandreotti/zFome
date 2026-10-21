using ZFome.Domain.Customers;

namespace ZFome.Application.Abstractions.Customers;

public interface ICustomerRepository
{
    Task<Customer?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken);
    Task<Customer?> FindByNormalizedPhoneAsync(Guid tenantId, Guid establishmentId, string normalizedPhone, CancellationToken cancellationToken);
    Task AddAsync(Customer customer, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ICustomerReadGateway
{
    Task<CustomerSearchResult> SearchAsync(
        Guid tenantId,
        Guid establishmentId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed record CustomerSearchResult(int Total, IReadOnlyList<CustomerReadModel> Items);
public sealed record CustomerReadModel(
    Guid Id,
    string Name,
    string Phone,
    string? Email,
    IReadOnlyList<CustomerAddressReadModel> Addresses);
public sealed record CustomerAddressReadModel(
    Guid Id,
    string Label,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    string PostalCode,
    bool IsPrimary);
