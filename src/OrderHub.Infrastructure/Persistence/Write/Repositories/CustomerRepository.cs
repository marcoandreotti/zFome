using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderHub.Application.Abstractions.Customers;
using OrderHub.Application.Exceptions;
using OrderHub.Domain.Customers;

namespace OrderHub.Infrastructure.Persistence.Write.Repositories;

/// <summary>
/// Representa um repositório de clientes que fornece métodos para obter, adicionar e salvar alterações de clientes no banco de dados.
/// </summary>
public sealed class CustomerRepository(OrderHubDbContext context) : ICustomerRepository
{
    public Task<Customer?> GetAsync(Guid tenantId, Guid establishmentId, Guid id, CancellationToken cancellationToken) =>
        context.Customers
            .Include(x => x.Addresses)
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.EstablishmentId == establishmentId && x.Id == id,
                cancellationToken);

    public Task<Customer?> FindByNormalizedPhoneAsync(
        Guid tenantId,
        Guid establishmentId,
        string normalizedPhone,
        CancellationToken cancellationToken) =>
        FindMatchingCustomerAsync(tenantId, establishmentId, normalizedPhone, cancellationToken);

    private async Task<Customer?> FindMatchingCustomerAsync(
        Guid tenantId,
        Guid establishmentId,
        string normalizedPhone,
        CancellationToken cancellationToken)
    {
        var candidates = Customer.GetPhoneLookupCandidates(normalizedPhone);
        var matches = await context.Customers
            .Include(x => x.Addresses)
            .Where(
                x => x.TenantId == tenantId
                    && x.EstablishmentId == establishmentId
                    && candidates.Contains(x.NormalizedPhone))
            .OrderBy(x => x.Id)
            .Take(2)
            .ToArrayAsync(cancellationToken);
        if (matches.Length > 1)
        {
            throw new ConflictException("Multiple customer records match this phone in the establishment.");
        }

        return matches.SingleOrDefault();
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        await context.Customers.AddAsync(customer, cancellationToken);
        await SaveChangesAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var promotedAddresses = context.ChangeTracker.Entries<CustomerAddress>()
                .Where(entry => entry.Property(address => address.IsPrimary).CurrentValue
                    && (entry.State == EntityState.Added || !entry.Property(address => address.IsPrimary).OriginalValue))
                .ToArray();

            if (promotedAddresses.Length == 0)
            {
                await context.SaveChangesAsync(cancellationToken);
                return;
            }

            var ownsTransaction = context.Database.CurrentTransaction is null;
            await using var transaction = ownsTransaction
                ? await context.Database.BeginTransactionAsync(cancellationToken)
                : null;
            foreach (var entry in promotedAddresses)
            {
                entry.Property(address => address.IsPrimary).CurrentValue = false;
            }

            await context.SaveChangesAsync(cancellationToken);
            foreach (var entry in promotedAddresses)
            {
                entry.Property(address => address.IsPrimary).CurrentValue = true;
            }

            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Customer was changed by another operation.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Customer data conflicts with an existing record in this establishment.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.CheckViolation })
        {
            throw new ConflictException("Customer data violates establishment integrity.");
        }
    }
}
