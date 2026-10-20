using Dapper;
using OrderHub.Application.Abstractions.Customers;
using OrderHub.Application.Abstractions.Persistence;

namespace OrderHub.Infrastructure.Persistence.Read;

/// <summary>
/// Representa um gateway de leitura para clientes, fornecendo métodos para pesquisar clientes e seus endereços associados no banco de dados.
/// </summary>
public sealed class CustomerReadGateway(IReadConnectionFactory connectionFactory) : ICustomerReadGateway
{
    public async Task<CustomerSearchResult> SearchAsync(
        Guid tenantId,
        Guid establishmentId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select count(*)
            from customers.customer c
            where c.tenant_id = @TenantId
              and c.establishment_id = @EstablishmentId
              and (@Search is null
                   or c.name ilike '%' || @Search || '%'
                   or c.normalized_phone like any(@NormalizedSearches)
                   or c.email ilike '%' || @Search || '%');

            select c.id, c.name, c.phone, c.email
            from customers.customer c
            where c.tenant_id = @TenantId
              and c.establishment_id = @EstablishmentId
              and (@Search is null
                   or c.name ilike '%' || @Search || '%'
                   or c.normalized_phone like any(@NormalizedSearches)
                   or c.email ilike '%' || @Search || '%')
            order by c.name, c.id
            offset @Offset rows fetch next @PageSize rows only;
            """;

        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : new string(search.Where(char.IsDigit).ToArray());
        var normalizedSearches = string.IsNullOrEmpty(normalizedSearch)
            ? Array.Empty<string>()
            : (normalizedSearch.Length is >= 8 and <= 15
                ? OrderHub.Domain.Customers.Customer.GetPhoneLookupCandidates(normalizedSearch)
                : [normalizedSearch])
            .Select(value => $"%{value}%")
            .ToArray();
        var parameters = new
        {
            TenantId = tenantId,
            EstablishmentId = establishmentId,
            Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            NormalizedSearches = normalizedSearches,
            Offset = (page - 1) * pageSize,
            PageSize = pageSize
        };
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var total = await grid.ReadSingleAsync<int>();
        var customers = (await grid.ReadAsync<CustomerRow>()).ToArray();

        if (customers.Length == 0)
        {
            return new CustomerSearchResult(total, []);
        }

        const string addressSql = """
            select a.id, a.customer_id as CustomerId, a.label, a.street, a.number,
                   a.complement, a.neighborhood, a.city, a.state,
                   a.postal_code as PostalCode, a.is_primary as IsPrimary
            from customers.customer_address a
            where a.tenant_id = @TenantId
              and a.establishment_id = @EstablishmentId
              and a.customer_id = any(@CustomerIds)
            order by a.customer_id, a.is_primary desc, a.label, a.id;
            """;
        var addresses = await connection.QueryAsync<AddressRow>(new CommandDefinition(
            addressSql,
            new { TenantId = tenantId, EstablishmentId = establishmentId, CustomerIds = customers.Select(x => x.Id).ToArray() },
            cancellationToken: cancellationToken));
        var byCustomer = addresses.ToLookup(x => x.CustomerId);
        var items = customers.Select(customer => new CustomerReadModel(
            customer.Id,
            customer.Name,
            customer.Phone,
            customer.Email,
            byCustomer[customer.Id].Select(address => new CustomerAddressReadModel(
                address.Id,
                address.Label,
                address.Street,
                address.Number,
                address.Complement,
                address.Neighborhood,
                address.City,
                address.State,
                address.PostalCode,
                address.IsPrimary)).ToArray())).ToArray();
        return new CustomerSearchResult(total, items);
    }

    private sealed record CustomerRow(Guid Id, string Name, string Phone, string? Email);
    private sealed record AddressRow(
        Guid Id,
        Guid CustomerId,
        string Label,
        string Street,
        string Number,
        string? Complement,
        string Neighborhood,
        string City,
        string State,
        string PostalCode,
        bool IsPrimary);
}
