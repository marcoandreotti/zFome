using ZFome.Application.Abstractions.Commands;
using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Identity.Management;
using ZFome.Application.Exceptions;
using ZFome.Domain.Identity;

namespace ZFome.Application.Identity.CreateAdministrativeUser;

public sealed class CreateAdministrativeUserCommandHandler(
    AdministrativeUserManagement management,
    IAdministrativeUserRepository repository,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : ICommandHandler<CreateAdministrativeUserCommand, Guid>
{
    public async Task<Guid> HandleAsync(CreateAdministrativeUserCommand command, CancellationToken cancellationToken)
    {
        var id = Guid.Empty;
        await management.ExecuteAsync(command.EstablishmentId, async (scope, actor, ct) =>
        {
            var tenantId = scope.TenantId;
            if (command.InitialRole == AdministrativeRole.Owner)
                AdministrativeUserManagement.RequireOwner(actor, Guid.Empty);
            var email = new Email(command.Email);
            if (await repository.EmailExistsAsync(tenantId, email.NormalizedValue, ct))
            {
                throw new ConflictException("The administrative user email is already in use in this tenant.");
            }

            var user = AdministrativeUser.Create(
                tenantId,
                command.Name,
                email,
                passwordHasher.Hash(command.Password),
                command.InitialRole,
                timeProvider.GetUtcNow());
            user.GrantEstablishmentAccess(scope.EstablishmentId, tenantId, timeProvider.GetUtcNow());
            await repository.AddAsync(user, ct);
            id = user.Id;
        }, cancellationToken);
        return id;
    }
}
