namespace ZFome.Domain.SharedKernel;

/// <summary>
/// Interface que define uma entidade que está associada a um estabelecimento específico, garantindo que cada instância da entidade tenha um identificador de estabelecimento único.
/// </summary>
public interface IEstablishmentScopedEntity : ITenantScopedEntity
{
    Guid EstablishmentId { get; }
}