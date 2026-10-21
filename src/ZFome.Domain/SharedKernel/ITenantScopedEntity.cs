namespace ZFome.Domain.SharedKernel;

/// <summary>
/// Interface que define uma entidade que está associada a um locatário específico, garantindo que cada instância da entidade tenha um identificador de locatário único.
/// </summary>
public interface ITenantScopedEntity
{
    Guid TenantId { get; }
}