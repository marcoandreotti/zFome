namespace ZFome.Application.Abstractions.Operations;

public interface IOperationsReadGateway
{
    Task<TableContext?> ResolveTableAsync(string normalizedSlug, string token, CancellationToken cancellationToken);
    Task<bool> IsOpenAsync(Guid tenantId, Guid establishmentId, DayOfWeek day, TimeOnly time, CancellationToken cancellationToken);
}

public sealed record TableContext(Guid TenantId, Guid EstablishmentId, Guid TableId, string Code);
