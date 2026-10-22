using ZFome.Application.Abstractions.Catalog;
using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Operations;
using ZFome.Application.Exceptions;
using ZFome.Application.Identity.Management;
using ZFome.Domain.Catalog;
using ZFome.Domain.Operations;

namespace ZFome.Application.Availability;

public sealed class ReplaceScheduleExceptionsHandler(AdministrativeUserManagement management, IAvailabilityRepository repository) : ICommandHandler<ReplaceScheduleExceptionsCommand>
{
    public Task HandleAsync(ReplaceScheduleExceptionsCommand command, CancellationToken cancellationToken) =>
        management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
        {
            var replacement = command.Exceptions.Select(x => x.IsOpen
                ? ServiceScheduleException.CreateOpen(scope.TenantId, scope.EstablishmentId, x.Date, x.ServiceType, x.OpensAt!.Value, x.ClosesAt!.Value, x.Reason)
                : ServiceScheduleException.CreateClosed(scope.TenantId, scope.EstablishmentId, x.Date, x.ServiceType, x.Reason)).ToArray();
            var duplicate = replacement.GroupBy(x => new { x.Date, x.ServiceType }).FirstOrDefault(x => x.Count() > 1);
            if (duplicate is not null) throw new ConflictException("Only one schedule exception is allowed per date and service type.");
            var previous = await repository.GetExceptionsAsync(scope.TenantId, scope.EstablishmentId, token);
            repository.ReplaceExceptions(previous, replacement);
            await repository.SaveChangesAsync(token);
        }, cancellationToken);
}

public sealed class PauseServiceHandler(AdministrativeUserManagement management, IAvailabilityRepository repository, TimeProvider timeProvider) :
    ICommandHandler<PauseServiceCommand>, ICommandHandler<ResumeServiceCommand>
{
    public Task HandleAsync(PauseServiceCommand command, CancellationToken cancellationToken) =>
        management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
        {
            var now = timeProvider.GetUtcNow();
            var active = await repository.GetActivePauseAsync(scope.TenantId, scope.EstablishmentId, command.ServiceType, now, token);
            if (active is not null) active.Cancel(now);
            repository.AddPause(ServicePause.Create(scope.TenantId, scope.EstablishmentId, command.ServiceType, now, command.EndsAt, command.Reason));
            await repository.SaveChangesAsync(token);
        }, cancellationToken);

    public Task HandleAsync(ResumeServiceCommand command, CancellationToken cancellationToken) =>
        management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
        {
            var now = timeProvider.GetUtcNow();
            var active = await repository.GetActivePauseAsync(scope.TenantId, scope.EstablishmentId, command.ServiceType, now, token)
                ?? throw new NotFoundException("Active service pause was not found.");
            active.Cancel(now);
            await repository.SaveChangesAsync(token);
        }, cancellationToken);
}

public sealed class OfferAvailabilityHandler(AdministrativeUserManagement management, IOfferAvailabilityRepository repository, TimeProvider timeProvider) :
    ICommandHandler<SetOfferUnavailabilityCommand>, ICommandHandler<ReactivateOfferCommand>
{
    public Task HandleAsync(SetOfferUnavailabilityCommand command, CancellationToken cancellationToken) =>
        management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
        {
            if (!await repository.OfferExistsAsync(scope.TenantId, scope.EstablishmentId, command.Kind, command.OfferId, token))
                throw new NotFoundException("Offer was not found.");
            var now = timeProvider.GetUtcNow();
            var active = await repository.GetActiveAsync(scope.TenantId, scope.EstablishmentId, command.Kind, command.OfferId, now, token);
            if (active is not null) active.Reactivate(now);
            repository.Add(OfferUnavailability.Create(scope.TenantId, scope.EstablishmentId, command.Kind, command.OfferId, now, command.EndsAt, command.Reason));
            await repository.SaveChangesAsync(token);
        }, cancellationToken);

    public Task HandleAsync(ReactivateOfferCommand command, CancellationToken cancellationToken) =>
        management.ExecuteAsync(command.EstablishmentId, async (scope, _, token) =>
        {
            var now = timeProvider.GetUtcNow();
            var active = await repository.GetActiveAsync(scope.TenantId, scope.EstablishmentId, command.Kind, command.OfferId, now, token)
                ?? throw new NotFoundException("Active offer unavailability was not found.");
            active.Reactivate(now);
            await repository.SaveChangesAsync(token);
        }, cancellationToken);
}
