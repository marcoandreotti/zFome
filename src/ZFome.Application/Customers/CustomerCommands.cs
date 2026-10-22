using ZFome.Application.Abstractions.Commands;

namespace ZFome.Application.Customers;

public sealed record UpsertCustomerCommand(
    Guid EstablishmentId,
    Guid? Id,
    string Name,
    string Phone,
    string? Email) : ICommand<Guid>;

public sealed record UpsertCustomerAddressCommand(
    Guid EstablishmentId,
    Guid CustomerId,
    Guid? AddressId,
    string Label,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    string PostalCode,
    bool IsPrimary) : ICommand<Guid>;

public sealed record RemoveCustomerAddressCommand(
    Guid EstablishmentId,
    Guid CustomerId,
    Guid AddressId) : ICommand;
