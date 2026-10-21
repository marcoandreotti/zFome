using ZFome.Application.Abstractions.Identity;
using ZFome.Application.Abstractions.Queries;
using ZFome.Application.Exceptions;
using ZFome.Domain.Identity;

namespace ZFome.Application.Identity.Authentication;

public sealed record GetAuthenticationContextQuery(string AccessToken) : IQuery<AuthenticationContext>;
public sealed record AuthenticationEstablishment(Guid Id, string Name);
public sealed record AuthenticationContext(bool PasswordChangeRequired, bool IsPlatformUser,
    IReadOnlyCollection<string> Capabilities, IReadOnlyCollection<AuthenticationEstablishment> Establishments);

public interface IAuthenticationContextGateway
{
    Task<IReadOnlyCollection<AuthenticationEstablishment>> GetEstablishmentsAsync(AuthenticatedIdentity identity, CancellationToken cancellationToken);
}

public sealed class GetAuthenticationContextQueryHandler(IAuthenticationSessionResolver resolver, IAuthenticationContextGateway gateway)
    : IQueryHandler<GetAuthenticationContextQuery, AuthenticationContext>
{
    public async Task<AuthenticationContext> HandleAsync(GetAuthenticationContextQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.AccessToken)) throw new UnauthorizedException("Session is invalid.");
        var identity = await resolver.ResolveAsync(query.AccessToken, cancellationToken)
            ?? throw new UnauthorizedException("Session is invalid.");
        var isPlatformUser = identity.Type == AuthenticationIdentityType.PlatformUser;
        // A sessão restrita informa somente o próximo passo; não expõe dados operacionais.
        if (identity.PasswordChangeRequired) return new(true, isPlatformUser, [], []);
        var capabilities = AdministrativePolicies.RoleMap
            .Where(policy => isPlatformUser || policy.Value.Any(identity.Roles.Contains))
            .Select(policy => policy.Key).Order(StringComparer.Ordinal).ToArray();
        return new(false, isPlatformUser, capabilities, await gateway.GetEstablishmentsAsync(identity, cancellationToken));
    }
}
