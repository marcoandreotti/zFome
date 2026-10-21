using ZFome.Domain.Ordering;

namespace ZFome.Application.Abstractions.Ordering;

public interface IKitchenDisplayReadGateway
{
    Task<IReadOnlyList<KitchenTicketReadModel>> GetQueueAsync(
        Guid tenantId,
        Guid establishmentId,
        CancellationToken cancellationToken);
}

public sealed record KitchenTicketReadModel(
    Guid Id,
    long Number,
    OrderServiceType ServiceType,
    OrderStatus Status,
    string? CustomerName,
    string? TableCode,
    DateTimeOffset ConfirmedAt,
    DateTimeOffset? PreparationStartedAt,
    KitchenTicketAction Action,
    IReadOnlyList<KitchenItemReadModel> Items);

public enum KitchenTicketAction
{
    StartPreparation,
    MarkReady
}

public sealed record KitchenItemReadModel(
    Guid Id,
    string ProductName,
    string? VariationName,
    decimal Quantity,
    string? Notes,
    IReadOnlyList<KitchenAdditionalReadModel> Additionals);

public sealed record KitchenAdditionalReadModel(string Name, decimal Quantity);
