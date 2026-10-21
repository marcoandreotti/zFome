using ZFome.Application.Abstractions.Customers;
using ZFome.Application.Abstractions.Queries;

namespace ZFome.Application.Customers;

public sealed record SearchCustomersQuery(
    Guid EstablishmentId,
    string? Search,
    int Page = 1,
    int PageSize = 20) : IQuery<CustomerSearchResult>;
