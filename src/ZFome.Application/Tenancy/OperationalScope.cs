namespace ZFome.Application.Tenancy;

public sealed record OperationalScope(Guid TenantId, Guid UserId, Guid EstablishmentId);
