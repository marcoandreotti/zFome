using ZFome.Application.Abstractions.Commands;
using ZFome.Domain.Catalog;
using ZFome.Domain.Ordering;

namespace ZFome.Application.Availability;

public sealed record ScheduleExceptionInput(DateOnly Date, OrderServiceType? ServiceType, bool IsOpen, TimeOnly? OpensAt, TimeOnly? ClosesAt, string? Reason);
public sealed record ReplaceScheduleExceptionsCommand(Guid EstablishmentId, IReadOnlyList<ScheduleExceptionInput> Exceptions) : ICommand;
public sealed record PauseServiceCommand(Guid EstablishmentId, OrderServiceType ServiceType, DateTimeOffset? EndsAt, string? Reason) : ICommand;
public sealed record ResumeServiceCommand(Guid EstablishmentId, OrderServiceType ServiceType) : ICommand;
public sealed record SetOfferUnavailabilityCommand(Guid EstablishmentId, OfferKind Kind, Guid OfferId, DateTimeOffset? EndsAt, string? Reason) : ICommand;
public sealed record ReactivateOfferCommand(Guid EstablishmentId, OfferKind Kind, Guid OfferId) : ICommand;
