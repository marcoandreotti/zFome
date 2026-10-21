using ZFome.Application.Abstractions.Commands;

namespace ZFome.Application.Tenancy.CreateEstablishment;

public sealed record CreateEstablishmentCommand(string TradeName, string Slug) : ICommand<Guid>;
