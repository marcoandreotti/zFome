using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Customers;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Exceptions;
using ZFome.Application.Tenancy;
using ZFome.Domain.Customers;

namespace ZFome.Application.Customers;

public sealed class UpsertCustomerCommandHandler(
    EstablishmentScopeResolver scopeResolver,
    ICustomerRepository repository,
    TimeProvider timeProvider) : ICommandHandler<UpsertCustomerCommand, Guid>
{
    public async Task<Guid> HandleAsync(UpsertCustomerCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken);
        var normalizedPhone = Customer.NormalizePhone(command.Phone);
        Customer? customer;

        if (command.Id is { } id)
        {
            customer = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, id, cancellationToken)
                ?? throw new NotFoundException("Customer was not found.");
            var conflicting = await repository.FindByNormalizedPhoneAsync(
                scope.TenantId,
                scope.EstablishmentId,
                normalizedPhone,
                cancellationToken);
            if (conflicting is not null && conflicting.Id != customer.Id)
            {
                throw new ConflictException("Customer phone is already in use in this establishment.");
            }
        }
        else
        {
            customer = await repository.FindByNormalizedPhoneAsync(
                scope.TenantId,
                scope.EstablishmentId,
                normalizedPhone,
                cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        if (customer is null)
        {
            customer = Customer.Create(scope.TenantId, scope.EstablishmentId, command.Name, command.Phone, command.Email, now);
            await repository.AddAsync(customer, cancellationToken);
        }
        else
        {
            customer.UpdateContact(command.Name, command.Phone, command.Email, now);
            await repository.SaveChangesAsync(cancellationToken);
        }

        return customer.Id;
    }
}

public sealed class UpsertCustomerAddressCommandHandler(
    EstablishmentScopeResolver scopeResolver,
    ICustomerRepository repository,
    TimeProvider timeProvider) : ICommandHandler<UpsertCustomerAddressCommand, Guid>
{
    public async Task<Guid> HandleAsync(UpsertCustomerAddressCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken);
        var customer = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, command.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer was not found.");
        var now = timeProvider.GetUtcNow();

        if (command.AddressId is { } addressId)
        {
            customer.UpdateAddress(addressId, command.Label, command.Street, command.Number, command.Complement, command.Neighborhood, command.City, command.State, command.PostalCode, command.IsPrimary, now);
        }
        else
        {
            addressId = customer.AddAddress(command.Label, command.Street, command.Number, command.Complement, command.Neighborhood, command.City, command.State, command.PostalCode, command.IsPrimary, now).Id;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return addressId;
    }
}

public sealed class RemoveCustomerAddressCommandHandler(
    EstablishmentScopeResolver scopeResolver,
    ICustomerRepository repository,
    TimeProvider timeProvider) : ICommandHandler<RemoveCustomerAddressCommand>
{
    public async Task HandleAsync(RemoveCustomerAddressCommand command, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(command.EstablishmentId, cancellationToken);
        var customer = await repository.GetAsync(scope.TenantId, scope.EstablishmentId, command.CustomerId, cancellationToken)
            ?? throw new NotFoundException("Customer was not found.");
        customer.RemoveAddress(command.AddressId, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SearchCustomersQueryHandler(
    EstablishmentScopeResolver scopeResolver,
    ICustomerReadGateway gateway) : IQueryHandler<SearchCustomersQuery, CustomerSearchResult>
{
    public async Task<CustomerSearchResult> HandleAsync(SearchCustomersQuery query, CancellationToken cancellationToken)
    {
        var scope = await scopeResolver.ResolveAsync(query.EstablishmentId, cancellationToken);
        return await gateway.SearchAsync(scope.TenantId, scope.EstablishmentId, query.Search, query.Page, query.PageSize, cancellationToken);
    }
}
