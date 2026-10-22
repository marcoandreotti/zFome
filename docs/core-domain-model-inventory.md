# Core domain model inventory

## Existing building blocks reused

- `ITenantScopedEntity` remains the marker used by EF Core tenant conventions.
- `ITenantContext` and `HttpTenantContext` remain the authenticated Tenant source; establishment scope extends this contract instead of replacing it.
- `zFomeDbContext` remains the write context and the module schema constants remain centralized in `DatabaseSchemas`.
- `IReadConnectionFactory` remains the Dapper connection boundary.
- The in-house command/query contracts and dispatchers remain the CQRS mechanism.
- `TimeProvider` from .NET is used where current time must be injected; no project-specific clock abstraction is introduced.
- `DomainException` remains the domain invariant error.

## New shared types justified by current capabilities

- `Money` is shared by catalog, orders, coupons, and payments.
- `Quantity` is shared by order items and item additions.

Typed identifiers are intentionally not introduced yet because existing tenancy and persistence contracts use `Guid`; introducing a parallel identifier hierarchy before two concrete implementations would duplicate conversion and mapping code.

## Persistence direction

The existing write context and Dapper connection factory are retained. Migration execution moves to `ZFome.Infrastructure.Migrations`; Domain and Application do not reference that project.
