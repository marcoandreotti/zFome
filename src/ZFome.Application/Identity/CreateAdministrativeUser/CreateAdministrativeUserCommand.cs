using ZFome.Application.Abstractions.Commands;
using ZFome.Domain.Identity;

namespace ZFome.Application.Identity.CreateAdministrativeUser;

public sealed record CreateAdministrativeUserCommand(
    string Name,
    string Email,
    string Password,
    AdministrativeRole InitialRole,
    Guid EstablishmentId = default) : ICommand<Guid>;
